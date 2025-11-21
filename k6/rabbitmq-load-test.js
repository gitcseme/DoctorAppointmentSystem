import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';
import { Counter, Rate, Trend } from 'k6/metrics';

// Custom metrics
const appointmentCreationRate = new Rate('appointment_creation_success_rate');
const appointmentCreationDuration = new Trend('appointment_creation_duration');
const queuedCounter = new Counter('appointments_queued');

// Load test configuration for 1000+ appointments/second
export const options = {
    scenarios: {
        // Ramp up to 1000 requests/second
        high_load: {
            executor: 'ramping-arrival-rate',
            startRate: 100,
            timeUnit: '1s',
            preAllocatedVUs: 100,
            maxVUs: 500,
            stages: [
                { duration: '30s', target: 500 }, // Ramp to 500/s
                { duration: '1m', target: 1000 },  // Ramp to 1000/s
                { duration: '2m', target: 1000 },  // Hold at 1000/s
                { duration: '30s', target: 1500 }, // Peak test: 1500/s
                { duration: '30s', target: 0 },    // Ramp down
            ],
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<100', 'p(99)<200'], // 95% under 100ms, 99% under 200ms
        appointment_creation_success_rate: ['rate>0.95'], // 95% success rate
        http_req_failed: ['rate<0.05'], // Less than 5% failures
    },
};

//Generate test data
const patientIds = SharedArray('patients', function () {
    const ids = [];
    for (let i = 1; i <= 100; i++) {
        ids.push(i);
    }
    return ids;
});

const doctorHospitalPairs = SharedArray('doctor_hospitals', function () {
    return [
        { doctorId: 1, hospitalId: 1 },
        { doctorId: 1, hospitalId: 2 },
        { doctorId: 2, hospitalId: 1 },
        { doctorId: 2, hospitalId: 2 },
        { doctorId: 3, hospitalId: 1 },
    ];
});

export default function () {
    const BASE_URL = __ENV.API_URL || 'http://localhost:5000';
    
    // Random test data
    const patient = patientIds[Math.floor(Math.random() * patientIds.length)];
    const dhPair = doctorHospitalPairs[Math.floor(Math.random() * doctorHospitalPairs.length)];
    
    // Future date for appointment
    const futureDate = new Date();
    futureDate.setDate(futureDate.getDate() + Math.floor(Math.random() * 30) + 1);
    const appointmentDate = futureDate.toISOString().split('T')[0];

    const payload = JSON.stringify({
        doctorId: dhPair.doctorId,
        hospitalId: dhPair.hospitalId,
        patientId: patient,
        appointmentDate: appointmentDate,
        notes: `Load test appointment - VU ${__VU} - Iter ${__ITER}`,
    });

    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    const startTime = Date.now();
    const response = http.post(`${BASE_URL}/api/appointments`, payload, params);
    const duration = Date.now() - startTime;

    // Record metrics
    appointmentCreationDuration.add(duration);

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

    appointmentCreationRate.add(success);
    if (success) {
        queuedCounter.add(1);
    }

    // Small sleep to prevent overwhelming the system
    sleep(0.01);
}

export function handleSummary(data) {
    return {
        'summary.json': JSON.stringify(data),
        stdout: textSummary(data, { indent: ' ', enableColors: true }),
    };
}

function textSummary(data, opts) {
    const indent = opts.indent || '';
    const colors = opts.enableColors;
    
    let summary = `\n${indent}================== Load Test Summary ==================\n`;
    summary += `${indent}Total Requests: ${data.metrics.http_reqs.values.count}\n`;
    summary += `${indent}Successful: ${Math.round(data.metrics.appointment_creation_success_rate.values.rate * 100)}%\n`;
    summary += `${indent}Appointments Queued: ${data.metrics.appointments_queued.values.count}\n`;
    summary += `${indent}Avg Duration: ${data.metrics.appointment_creation_duration.values.avg.toFixed(2)}ms\n`;
    summary += `${indent}P95 Duration: ${data.metrics.appointment_creation_duration.values['p(95)'].toFixed(2)}ms\n`;
    summary += `${indent}P99 Duration: ${data.metrics.appointment_creation_duration.values['p(99)'].toFixed(2)}ms\n`;
    summary += `${indent}========================================================\n`;
    
    return summary;
}
