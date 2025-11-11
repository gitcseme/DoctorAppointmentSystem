import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate } from 'k6/metrics';

const duplicateSerials = new Counter('duplicate_serials');
const successRate = new Rate('success_rate');
const dailyLimitHit = new Counter('daily_limit_hit');

export const options = {
    stages: [
        { duration: '30s', target: 20 },
        { duration: '1m', target: 50 },
        { duration: '30s', target: 100 },
        { duration: '1m', target: 50 },
        { duration: '30s', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<1000'],
        http_req_failed: ['rate<0.1'],
        success_rate: ['rate>0.70'],
        duplicate_serials: ['count<1'],
    },
};

const BASE_URL = __ENV.API_URL || 'https://localhost:7123';

// Predictable data structure:
// - 500 hospitals (IDs: 1-500)
// - 25,000 doctors (IDs: 1-25000)
// - Each hospital has exactly 50 doctors
// - Hospital 1: Doctors 1-50, Hospital 2: Doctors 51-100, etc.
// - 100,000 patients (IDs: 1-100000)
// - Daily limit: 50 per doctor-hospital

const TOTAL_HOSPITALS = 500;
const DOCTORS_PER_HOSPITAL = 50;
const TOTAL_PATIENTS = 100000;

const serialsByKey = {};

export default function () {
    // Select random hospital (1-500)
    const hospitalId = Math.floor(Math.random() * TOTAL_HOSPITALS) + 1;
    
    // Calculate doctor range for this hospital
    // Hospital 1: Doctors 1-50, Hospital 2: Doctors 51-100, etc.
    const firstDoctorId = (hospitalId - 1) * DOCTORS_PER_HOSPITAL + 1;
    
    // Select random doctor from this hospital's doctors
    const doctorId = Math.floor(Math.random() * DOCTORS_PER_HOSPITAL) + firstDoctorId;
    
    // Select random patient (1-100000)
    const patientId = Math.floor(Math.random() * TOTAL_PATIENTS) + 1;
    
    // Use tomorrow's date
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    const date = tomorrow.toISOString().split('T')[0];

    const payload = JSON.stringify({
        doctorId: doctorId,
        hospitalId: hospitalId,
        patientId: patientId,
        appointmentDate: date,
        notes: `Load test VU:${__VU} Iter:${__ITER}`,
    });

    const res = http.post(`${BASE_URL}/api/appointments`, payload, {
        headers: { 'Content-Type': 'application/json' },
    });

    const success = check(res, {
        'status 201': (r) => r.status === 201,
        'status 404': (r) => r.status === 404,
        'status 409': (r) => r.status === 409,
    });

    if (res.status === 201) {
        successRate.add(true);
        
        try {
            const body = JSON.parse(res.body);
            const appointmentId = body.id;
            
            if (appointmentId) {
                sleep(0.05);
                
                const getRes = http.get(`${BASE_URL}/api/appointments/${appointmentId}`);
                if (getRes.status === 200) {
                    const apt = JSON.parse(getRes.body);
                    const key = `${doctorId}_${hospitalId}_${date}`;
                    
                    if (!serialsByKey[key]) {
                        serialsByKey[key] = new Set();
                    }
                    
                    if (serialsByKey[key].has(apt.serialNumber)) {
                        duplicateSerials.add(1);
                        console.error(`❌ DUPLICATE SERIAL: Doctor ${doctorId}, Hospital ${hospitalId}, Date ${date}, Serial ${apt.serialNumber}`);
                    } else {
                        serialsByKey[key].add(apt.serialNumber);
                    }
                }
            }
        } catch (e) {
            console.error(`Error parsing response: ${e.message}`);
        }
    }
    else if (res.status === 409) {
        successRate.add(true);
        
        try {
            const body = JSON.parse(res.body);
            if (body.message && body.message.includes('Daily patient limit')) {
                dailyLimitHit.add(1);
            }
        } catch (e) {}
    } else if (res.status === 404) {
        successRate.add(false);
        console.warn(`404 Not Found: Doctor ${doctorId}, Hospital ${hospitalId}, Patient ${patientId}`);
    } else {
        successRate.add(false);
        console.error(`Unexpected status ${res.status}: ${res.body}`);
    }

    sleep(Math.random() * 0.2 + 0.1);
}

