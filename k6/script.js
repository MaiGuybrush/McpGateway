import http from 'k6/http';
import { check, sleep } from 'k6';
import { htmlReport } from "https://raw.githubusercontent.com/benc-uk/k6-reporter/main/bundle.js";

// Test configuration
const BASE_URL = 'http://localhost:5000'; // Adjust based on actual MCP server URL
const TOOL_TEST_CONFIG = {
  manual: {
    getUserDetails: 'get_user_details',
    placeNewOrder: 'place_new_order'
  },
  automated: {
    getUserDetails: 'GetUserById', // Adjust based on actual generated tool names
    placeNewOrder: 'CreateOrder'
  }
};

// Test data
const testData = {
  userId: '550e8400-e29b-41d4-a716-446655440000',
  customerId: '550e8400-e29b-41d4-a716-446655440001',
  productsJson: JSON.stringify([{
    productId: 'PROD-001',
    quantity: 2,
    unitPrice: 99.99
  }]),
  shippingAddress: '台北市信義區信義路五段7號',
  recipientName: '測試用戶',
  recipientPhone: '+886-912-345-678',
  shippingMethod: 'standard',
  paymentMethod: 'credit_card'
};

// Options
export const options = {
  stages: [
    { duration: '30s', target: 10 }, // 10 VUs for 30s
    { duration: '1m', target: 20 },  // Scale to 20 VUs
    { duration: '2m', target: 50 },  // Scale to 50 VUs
    { duration: '30s', target: 0 },  // Scale down
  ],
  thresholds: {
    'http_req_duration{p95}': ['p(95)<50'], // 95th percentile < 50ms
    'http_req_failed': ['rate<0.01'], // Error rate < 1%
  },
};

// Helper function to call MCP tool
function callMcpTool(toolName, arguments) {
  const payload = {
    jsonrpc: '2.0',
    id: Math.floor(Math.random() * 1000000),
    method: 'tools/call',
    params: {
      name: toolName,
      arguments: arguments
    }
  };
  
  return http.post(`${BASE_URL}/mcp`, JSON.stringify(payload), {
    headers: {
      'Content-Type': 'application/json',
    },
    timeout: '30s',
  });
}

// Test scenarios
export function manualGetUserDetails() {
  const res = callMcpTool(TOOL_TEST_CONFIG.manual.getUserDetails, {
    userId: testData.userId,
    fields: 'all',
    includeDeleted: false
  });
  
  check(res, {
    'manual_get_user_details status is 200': (r) => r.status === 200,
    'manual_get_user_details response contains result': (r) => r.body.includes('result'),
  });
  
  sleep(0.1);
}

export function manualPlaceNewOrder() {
  const res = callMcpTool(TOOL_TEST_CONFIG.manual.placeNewOrder, {
    customerId: testData.customerId,
    productsJson: testData.productsJson,
    shippingMethod: testData.shippingMethod,
    paymentMethod: testData.paymentMethod,
    recipientName: testData.recipientName,
    recipientPhone: testData.recipientPhone,
    shippingAddress: testData.shippingAddress
  });
  
  check(res, {
    'manual_place_new_order status is 200': (r) => r.status === 200,
    'manual_place_new_order response contains result': (r) => r.body.includes('result'),
  });
  
  sleep(0.1);
}

export function automatedGetUserDetails() {
  const res = callMcpTool(TOOL_TEST_CONFIG.automated.getUserDetails, {
    userId: testData.userId
  });
  
  check(res, {
    'automated_get_user_details status is 200': (r) => r.status === 200,
    'automated_get_user_details response contains result': (r) => r.body.includes('result'),
  });
  
  sleep(0.1);
}

export function automatedPlaceNewOrder() {
  const res = callMcpTool(TOOL_TEST_CONFIG.automated.placeNewOrder, {
    customerId: testData.customerId,
    requestBody: {
      products: JSON.parse(testData.productsJson),
      shipping: {
        method: testData.shippingMethod,
        address: testData.shippingAddress
      },
      payment: {
        method: testData.paymentMethod
      }
    }
  });
  
  check(res, {
    'automated_place_new_order status is 200': (r) => r.status === 200,
    'automated_place_new_order response contains result': (r) => r.body.includes('result'),
  });
  
  sleep(0.1);
}

// Main test execution
export default function() {
  // Alternate between different tool tests
  const scenario = Math.random();
  
  if (scenario < 0.25) {
    manualGetUserDetails();
  } else if (scenario < 0.5) {
    manualPlaceNewOrder();
  } else if (scenario < 0.75) {
    automatedGetUserDetails();
  } else {
    automatedPlaceNewOrder();
  }
}

// HTML report generation
export function handleSummary(data) {
  return {
    'k6/report.html': htmlReport(data),
  };
}