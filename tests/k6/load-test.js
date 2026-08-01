import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

export const options = {
  stages: [
    { duration: '30s', target: 10 },  // Ramp up to 10 VUs
    { duration: '1m', target: 10 },   // Stay at 10 VUs
    { duration: '30s', target: 50 },  // Ramp up to 50 VUs
    { duration: '2m', target: 50 },   // Stay at 50 VUs
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    'http_req_duration': ['p(95)<50', 'p(99)<100'], // 95th percentile < 50ms, 99th < 100ms
    'http_req_duration{scenario:mechanical}': ['p(95)<50'],
    'http_req_duration{scenario:manual}': ['p(95)<50'],
    'errors': ['rate<0.01'], // Error rate < 1%
  },
};

const BASE_URL = 'http://localhost:5000'; // MCP Gateway URL

// Test data
const userIds = [123, 456, 789, 101, 202, 303, 404, 505, 606, 707];
const customerIds = [2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008, 2009, 2010];

export function setup() {
  // Start with a single request to warm up
  http.get(`${BASE_URL}/health`);
  sleep(1);
  
  return { timestamp: new Date().toISOString() };
}

export default function (data) {
  const scenario = Math.random() < 0.5 ? 'mechanical' : 'manual';
  
  // Alternate between UserQuery and OrderCreate operations
  if (Math.random() < 0.6) {
    // UserQuery test
    testUserQuery(scenario);
  } else {
    // OrderCreate test
    testOrderCreate(scenario);
  }
  
  sleep(0.5 + Math.random() * 0.5); // 0.5-1s think time
}

function testUserQuery(scenario) {
  const userId = userIds[Math.floor(Math.random() * userIds.length)];
  const includeDetails = Math.random() < 0.3;
  
  // Construct MCP tool call payload
  const payload = {
    jsonrpc: "2.0",
    id: Math.floor(Math.random() * 1000000),
    method: "tools/call",
    params: {
      name: scenario === 'mechanical' ? 'get_api_users_id' : 'get_user_details',
      arguments: {
        userId: userId,
        ...(scenario === 'manual' && includeDetails && { includeProfileDetails: true })
      }
    }
  };
  
  const res = http.post(`${BASE_URL}/mcp`, JSON.stringify(payload), {
    headers: { 'Content-Type': 'application/json' },
    tags: { scenario: scenario, tool: 'user_query' },
  });
  
  const success = check(res, {
    'UserQuery status is 200': (r) => r.status === 200,
    'UserQuery response has result': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.result !== undefined;
      } catch {
        return false;
      }
    },
    'UserQuery latency < 50ms': (r) => r.timings.duration < 50,
  });
  
  errorRate.add(!success, { tags: { tool: 'user_query' } });
}

function testOrderCreate(scenario) {
  const customerId = customerIds[Math.floor(Math.random() * customerIds.length)];
  const itemCount = Math.floor(Math.random() * 3) + 1; // 1-3 items
  const shippingSpeed = Math.random() < 0.2 ? 'Express' : 'Standard';
  
  const items = [];
  for (let i = 0; i < itemCount; i++) {
    items.push({
      productId: `PROD-${Math.floor(Math.random() * 1000)}`,
      productName: `Product ${Math.floor(Math.random() * 100)}`,
      quantity: Math.floor(Math.random() * 5) + 1,
      unitPrice: parseFloat((Math.random() * 100 + 9.99).toFixed(2))
    });
  }
  
  const payload = scenario === 'mechanical' ? {
    jsonrpc: "2.0",
    id: Math.floor(Math.random() * 1000000),
    method: "tools/call",
    params: {
      name: 'post_api_orders',
      arguments: {
        requestBody: {
          userId: customerId,
          items: items,
          shippingAddress: {
            street: `${Math.floor(Math.random() * 1000)} Test St`,
            city: 'Test City',
            state: 'MA',
            zipCode: '02101',
            country: 'USA'
          },
          priority: shippingSpeed
        }
      }
    }
  } : {
    jsonrpc: "2.0",
    id: Math.floor(Math.random() * 1000000),
    method: "tools/call",
    params: {
      name: 'place_new_order',
      arguments: {
        customerId: customerId,
        orderItems: items,
        deliveryAddress: {
          street: `${Math.floor(Math.random() * 1000)} Test St`,
          city: 'Test City',
          state: 'MA',
          zipCode: '02101',
          country: 'USA'
        },
        shippingSpeed: shippingSpeed
      }
    }
  };
  
  const res = http.post(`${BASE_URL}/mcp`, JSON.stringify(payload), {
    headers: { 'Content-Type': 'application/json' },
    tags: { scenario: scenario, tool: 'order_create' },
  });
  
  const success = check(res, {
    'OrderCreate status is 200': (r) => r.status === 200,
    'OrderCreate response has orderId': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.result?.order?.orderId !== undefined;
      } catch {
        return false;
      }
    },
    'OrderCreate latency < 100ms': (r) => r.timings.duration < 100,
  });
  
  errorRate.add(!success, { tags: { tool: 'order_create' } });
}

export function handleSummary(data) {
  const mechanicalUserQuery = data.metrics['http_req_duration{scenario:mechanical,tool:user_query}'];
  const manualUserQuery = data.metrics['http_req_duration{scenario:manual,tool:user_query}'];
  const mechanicalOrderCreate = data.metrics['http_req_duration{scenario:mechanical,tool:order_create}'];
  const manualOrderCreate = data.metrics['http_req_duration{scenario:manual,tool:order_create}'];
  
  const report = {
    timestamp: new Date().toISOString(),
    summary: {
      vus_max: data.metrics.vus_max.values.max,
      iteration_count: data.metrics.iteration_count.values.count,
      error_rate: data.metrics.errors.values.rate * 100,
    },
    latencies: {
      mechanical: {
        userQuery: {
          p50: mechanicalUserQuery?.values['p(50)'] || 0,
          p95: mechanicalUserQuery?.values['p(95)'] || 0,
          p99: mechanicalUserQuery?.values['p(99)'] || 0,
        },
        orderCreate: {
          p50: mechanicalOrderCreate?.values['p(50)'] || 0,
          p95: mechanicalOrderCreate?.values['p(95)'] || 0,
          p99: mechanicalOrderCreate?.values['p(99)'] || 0,
        }
      },
      manual: {
        userQuery: {
          p50: manualUserQuery?.values['p(50)'] || 0,
          p95: manualUserQuery?.values['p(95)'] || 0,
          p99: manualUserQuery?.values['p(99)'] || 0,
        },
        orderCreate: {
          p50: manualOrderCreate?.values['p(50)'] || 0,
          p95: manualOrderCreate?.values['p(95)'] || 0,
          p99: manualOrderCreate?.values['p(99)'] || 0,
        }
      }
    }
  };
  
  return {
    'k6-test-results.json': JSON.stringify(report, null, 2),
    'stdout': `
      Performance Test Results
      ========================
      
      Mechanical UserQuery:
        p50: ${mechanicalUserQuery?.values['p(50)']?.toFixed(2) || 'N/A'}ms
        p95: ${mechanicalUserQuery?.values['p(95)']?.toFixed(2) || 'N/A'}ms
        p99: ${mechanicalUserQuery?.values['p(99)']?.toFixed(2) || 'N/A'}ms
      
      Manual UserQuery:
        p50: ${manualUserQuery?.values['p(50)']?.toFixed(2) || 'N/A'}ms
        p95: ${manualUserQuery?.values['p(95)']?.toFixed(2) || 'N/A'}ms
        p99: ${manualUserQuery?.values['p(99)']?.toFixed(2) || 'N/A'}ms
      
      Mechanical OrderCreate:
        p50: ${mechanicalOrderCreate?.values['p(50)']?.toFixed(2) || 'N/A'}ms
        p95: ${mechanicalOrderCreate?.values['p(95)']?.toFixed(2) || 'N/A'}ms
        p99: ${mechanicalOrderCreate?.values['p(99)']?.toFixed(2) || 'N/A'}ms
      
      Manual OrderCreate:
        p50: ${manualOrderCreate?.values['p(50)']?.toFixed(2) || 'N/A'}ms
        p95: ${manualOrderCreate?.values['p(95)']?.toFixed(2) || 'N/A'}ms
        p99: ${manualOrderCreate?.values['p(99)']?.toFixed(2) || 'N/A'}ms
      
      Error Rate: ${(data.metrics.errors.values.rate * 100).toFixed(2)}%
      
      ${data.metrics.errors.values.rate > 0.01 ? 
        '⚠️  ERROR RATE EXCEEDS 1% THRESHOLD' : 
        '✅ All thresholds met'}
    `
  };
}