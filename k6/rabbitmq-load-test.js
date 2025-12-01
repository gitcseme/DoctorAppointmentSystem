import http from 'k6/http';
import { check, sleep } from 'k6';

// Load test configuration for 1000+ appointments/second
export const options = {
    //scenarios: {
    //    // Ramp up to 1000 requests/second
    //    high_load: {
    //        executor: 'ramping-arrival-rate',
    //        startRate: 100,
    //        timeUnit: '1s',
    //        preAllocatedVUs: 100,
    //        maxVUs: 500,
    //        stages: [
    //            { duration: '30s', target: 20 }, // Ramp to 500/s
    //            { duration: '1m', target:  50 },  // Ramp to 1000/s
    //            { duration: '2m', target:  100 },  // Hold at 1000/s
    //            { duration: '30s', target: 50 }, // Peak test: 1500/s
    //            { duration: '30s', target: 20 },    // Ramp down
    //        ],
    //    },
    //},
    stages: [
        { duration: '30s', target: 20 }, // Ramp to 500/s
        { duration: '30s', target: 50 },  // Ramp to 1000/s
        { duration: '30s', target: 100 },  // Hold at 1000/s
        { duration: '30s', target: 50 }, // Peak test: 1500/s
        { duration: '30s', target: 20 },    // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<100', 'p(99)<200'], // 95% under 100ms, 99% under 200ms
        http_req_failed: ['rate<0.05'], // Less than 5% failures
    },
};

const BASE_URL = __ENV.API_URL || 'https://localhost:7123';

// Predictable data structure:
// - 10 hospitals (IDs: 1-10)
// - 500 doctors (IDs: 1-500)
// - Each hospital has exactly 50 doctors
// - Hospital 1: Doctors 1-50, Hospital 2: Doctors 51-100, etc.
// - 100,000 patients (IDs: 1-100000)
// - Daily limit: 50 per doctor-hospital

const TOTAL_HOSPITALS = 10;
const DOCTORS_PER_HOSPITAL = 50;
const TOTAL_PATIENTS = 100000;

export default function () {
    // Select random hospital (1-10)
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
    const appointmentDate = tomorrow.toISOString().split('T')[0];

    const payload = JSON.stringify({
        doctorId: doctorId,
        hospitalId: hospitalId,
        patientId: patientId,
        appointmentDate: appointmentDate,
        notes: `Load test VU:${__VU} Iter:${__ITER}`,
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    const startTime = Date.now();
    const response = http.post(`${BASE_URL}/api/appointments`, payload, params);
    const duration = Date.now() - startTime;


    const success = check(response, {
        'status is 202 (Accepted)': (r) => r.status === 202,
        'has appointment reference': (r) => {
            try {
                const body = JSON.parse(r.body);
                return body.appointmentReference !== undefined;
            } catch {
                return false;
            }
        },
        'response time < 100ms': () => duration < 100,
    });


    // Small sleep to prevent overwhelming the system
    sleep(Math.random() * 0.2 + 0.1);
}

