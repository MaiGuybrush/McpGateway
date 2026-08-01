# 測試 Prompt 設計 (20 個)

## UserQuery Tool 測試 (10 個)

### 1. 基本查詢
**Prompt**: "Get user information for user ID 123"
**預期**: 應呼叫 get_api_users_id，參數 id=123

### 2. 包含詳細資訊
**Prompt**: "Fetch detailed user information for user 456 including all additional details"
**預期**: 應呼叫 get_api_users_id，參數 id=456, includeDetails=true

### 3. 自然語言查詢
**Prompt**: "Can you tell me about user number 789? I want to see their full profile."
**預期**: 應呼叫 get_api_users_id，參數 id=789, includeDetails=true

### 4. 間接參數提取
**Prompt**: "The user with identifier 101 needs to be reviewed. Pull up their account."
**預期**: 應呼叫 get_api_users_id，參數 id=101

### 5. 多步驟邏輯
**Prompt**: "First, get user 202's basic info. Then check if we need more details."
**預期**: 應呼叫 get_api_users_id，參數 id=202, includeDetails=false (或 true)

### 6. 模糊描述
**Prompt**: "Look up the customer record for user three hundred three"
**預期**: 應呼叫 get_api_users_id，參數 id=303

### 7. 技術語言
**Prompt**: "Query the user API endpoint with ID parameter set to 404 and enable detailed flag"
**預期**: 應呼叫 get_api_users_id，參數 id=404, includeDetails=true

### 8. 商業情境
**Prompt**: "Our VIP customer (user ID 505) called support. Get their account details immediately."
**預期**: 應呼叫 get_api_users_id，參數 id=505, includeDetails=true

### 9. 問句形式
**Prompt**: "What information do we have about user 606 in our system?"
**預期**: 應呼叫 get_api_users_id，參數 id=606

### 10. 條件邏輯
**Prompt**: "If user 707 exists, retrieve their full profile with all details."
**預期**: 應呼叫 get_api_users_id，參數 id=707, includeDetails=true

## OrderCreate Tool 測試 (10 個)

### 11. 基本訂單
**Prompt**: "Create an order for user 1001 with one item: product ABC, quantity 2, price 9.99"
**預期**: 應呼叫 post_api_orders，包含 userId, items, shippingAddress

### 12. 多商品訂單
**Prompt**: "Place an order for user 1002: 3x Widget ($5.99 each) and 1x Gadget ($19.99), ship to 123 Main St, Boston, MA 02101, USA"
**預期**: 應正確解析多個商品和地址

### 13. 加急訂單
**Prompt**: "User 1003 needs an EXPRESS order: 1x Emergency Kit, $29.99, ship to 456 Oak Ave, New York, NY 10001"
**預期**: 應設定 priority="Express"

### 14. 自然語言描述
**Prompt**: "Can you help customer 1004 place an order? They want two blue t-shirts at $12.50 each, and send it to their home in Seattle."
**預期**: 應解析出商品、數量、價格和地址資訊

### 15. 含備註的訂單
**Prompt**: "Create order for user 1005: product X100, quantity 1, price $99.99. Notes: 'Gift wrap requested'. Address: 789 Pine Rd, Chicago, IL"
**預期**: 應包含 notes 欄位

### 16. 技術規格
**Prompt**: "POST to orders endpoint: userId=1006, items=[{productId:'P001',productName:'Laptop',quantity:1,unitPrice:899.99}], shippingAddress={street:'1 Tech Drive',city:'San Francisco',state:'CA',zipCode:'94105',country:'USA'}"
**預期**: 應正確解析 JSON-like 結構

### 17. 商業情境
**Prompt**: "Our wholesale client (user 1007) is placing a bulk order: 50x Pen at $0.50 each and 30x Notebook at $2.00. Ship to their office in Austin, TX."
**預期**: 應處理大數量和多商品

### 18. 最小資訊訂單
**Prompt**: "Quick order for user 1008: product Y200, qty 1, $50"
**預期**: 應使用預設值填補缺失的地址資訊或標記為錯誤

### 19. 國際訂單
**Prompt**: "User 1009 in Canada wants 2x Maple Syrup at CAD$15.00 each. Ship to 321 Maple Lane, Toronto, ON M5V 1A1, Canada"
**預期**: 應正確處理國際地址格式

### 20. 模糊但合理的請求
**Prompt**: "Help user 1010 buy some stuff: they need a coffee mug (price $8.99) and maybe a coaster set if available."
**預期**: 應至少正確處理主要商品，或可選商品作為備註