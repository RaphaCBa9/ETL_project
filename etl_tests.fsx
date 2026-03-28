// ============================================================================
// ETL Project - Comprehensive Test Suite for Pure Functions
// Aluno: Raphael Cavalcanti Banov
// Email: raphaelb3@al.insper.edu.br
// ============================================================================

open System

// ============================================================================
// SECTION 1: RECORD TYPES (duplicated from main script)
// ============================================================================

type Order = {
    id: int
    client_id: int
    order_date: DateTime
    status: string
    origin: string
}

type OrderItem = {
    order_id: int
    product_id: int
    quantity: float
    price: float
    tax: float
}

type OrderSummary = {
    order_id: int
    total_amount: float
    total_taxes: float
}

type MonthlySummary = {
    year: int
    month: int
    average_amount: float
    average_taxes: float
    order_count: int
}

// ============================================================================
// SECTION 2: PURE FUNCTIONS (duplicated from main script)
// ============================================================================

let parseIntOption (str: string) : int option =
    match Int32.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

let parseFloatOption (str: string) : float option =
    match Double.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

let parseDateTimeOption (str: string) : DateTime option =
    match DateTime.TryParse(str) with
    | (true, value) -> Some value
    | (false, _) -> None

let trim (str: string) : string =
    str.Trim()

let splitCsvLine (line: string) : string array =
    line.Split(',') |> Array.map trim

let lineToOrder (line: string) : Order option =
    let fields = splitCsvLine line
    if fields.Length < 5 then None
    else
        match (parseIntOption fields.[0], parseIntOption fields.[1], parseDateTimeOption fields.[2]) with
        | (Some id, Some client_id, Some order_date) ->
            Some {
                id = id
                client_id = client_id
                order_date = order_date
                status = fields.[3]
                origin = fields.[4]
            }
        | _ -> None

let lineToOrderItem (line: string) : OrderItem option =
    let fields = splitCsvLine line
    if fields.Length < 5 then None
    else
        match (parseIntOption fields.[0], parseIntOption fields.[1], parseFloatOption fields.[2], 
               parseFloatOption fields.[3], parseFloatOption fields.[4]) with
        | (Some order_id, Some product_id, Some quantity, Some price, Some tax) ->
            Some {
                order_id = order_id
                product_id = product_id
                quantity = quantity
                price = price
                tax = tax
            }
        | _ -> None

let calculateItemRevenue (item: OrderItem) : float =
    item.quantity * item.price

let calculateItemTax (item: OrderItem) : float =
    (calculateItemRevenue item) * item.tax

let filterOrdersByStatusAndOrigin (status: string option) (origin: string option) (order: Order) : bool =
    let statusMatch = match status with
                      | None -> true
                      | Some s -> order.status.ToLower() = s.ToLower()
    let originMatch = match origin with
                      | None -> true
                      | Some o -> order.origin.ToLower() = o.ToLower()
    statusMatch && originMatch

let innerJoinOrdersAndItems (orders: Order list) (items: OrderItem list) : (Order * OrderItem) list =
    orders
    |> List.collect (fun order ->
        items
        |> List.filter (fun item -> item.order_id = order.id)
        |> List.map (fun item -> (order, item))
    )

let aggregateOrderTotals (joinedData: (Order * OrderItem) list) : OrderSummary list =
    joinedData
    |> List.groupBy (fun (order, _) -> order.id)
    |> List.map (fun (orderId, group) ->
        let totalAmount = 
            group
            |> List.map (fun (_, item) -> calculateItemRevenue item)
            |> List.fold (+) 0.0
        
        let totalTaxes = 
            group
            |> List.map (fun (_, item) -> calculateItemTax item)
            |> List.fold (+) 0.0
        
        {
            order_id = orderId
            total_amount = totalAmount
            total_taxes = totalTaxes
        }
    )

let processETL (orders: Order list) (items: OrderItem list) (statusFilter: string option) (originFilter: string option) : OrderSummary list =
    orders
    |> List.filter (filterOrdersByStatusAndOrigin statusFilter originFilter)
    |> fun filteredOrders -> innerJoinOrdersAndItems filteredOrders items
    |> aggregateOrderTotals
    |> List.sortBy (fun summary -> summary.order_id)

let calculateMonthlySummaries (orders: Order list) (summaries: OrderSummary list) : MonthlySummary list =
    let orderMap = orders |> List.map (fun o -> (o.id, o)) |> Map.ofList
    
    summaries
    |> List.choose (fun summary ->
        match Map.tryFind summary.order_id orderMap with
        | Some order ->
            Some (order.order_date.Year, order.order_date.Month, summary)
        | None -> None
    )
    |> List.groupBy (fun (year, month, _) -> (year, month))
    |> List.map (fun ((year, month), group) ->
        let count = group.Length
        let totalAmount = group |> List.sumBy (fun (_, _, s) -> s.total_amount)
        let totalTaxes = group |> List.sumBy (fun (_, _, s) -> s.total_taxes)
        
        {
            year = year
            month = month
            average_amount = totalAmount / float count
            average_taxes = totalTaxes / float count
            order_count = count
        }
    )
    |> List.sortBy (fun m -> (m.year, m.month))

// ============================================================================
// SECTION 3: TEST FRAMEWORK
// ============================================================================

let mutable testsPassed = 0
let mutable testsFailed = 0

let assertEqual<'T when 'T : equality> (actual: 'T) (expected: 'T) (testName: string) : unit =
    if actual = expected then
        printfn "✓ PASS: %s" testName
        testsPassed <- testsPassed + 1
    else
        printfn "✗ FAIL: %s" testName
        printfn "  Expected: %A" expected
        printfn "  Actual: %A" actual
        testsFailed <- testsFailed + 1

let assertFloatEqual (actual: float) (expected: float) (tolerance: float) (testName: string) : unit =
    if abs (actual - expected) <= tolerance then
        printfn "✓ PASS: %s" testName
        testsPassed <- testsPassed + 1
    else
        printfn "✗ FAIL: %s" testName
        printfn "  Expected: %.2f" expected
        printfn "  Actual: %.2f" actual
        testsFailed <- testsFailed + 1

let assertTrue (condition: bool) (testName: string) : unit =
    if condition then
        printfn "✓ PASS: %s" testName
        testsPassed <- testsPassed + 1
    else
        printfn "✗ FAIL: %s" testName
        testsFailed <- testsFailed + 1

// ============================================================================
// SECTION 4: TEST SUITES
// ============================================================================

// ============================================================================
// Test Suite 1: Parsing Functions
// ============================================================================

printfn "\n========== TEST SUITE 1: PARSING FUNCTIONS =========="

// Test parseIntOption
assertEqual (parseIntOption "123") (Some 123) "parseIntOption: Valid integer"
assertEqual (parseIntOption "0") (Some 0) "parseIntOption: Zero"
assertEqual (parseIntOption "-42") (Some -42) "parseIntOption: Negative integer"
assertEqual (parseIntOption "abc") None "parseIntOption: Invalid string"
assertEqual (parseIntOption "") None "parseIntOption: Empty string"

// Test parseFloatOption
assertEqual (parseFloatOption "123.45") (Some 123.45) "parseFloatOption: Valid float"
assertEqual (parseFloatOption "0.0") (Some 0.0) "parseFloatOption: Zero"
assertEqual (parseFloatOption "-42.5") (Some -42.5) "parseFloatOption: Negative float"
assertEqual (parseFloatOption "abc") None "parseFloatOption: Invalid string"
assertEqual (parseFloatOption "") None "parseFloatOption: Empty string"

// Test parseDateTimeOption
(match parseDateTimeOption "2024-10-02T03:05:39" with
    | Some dt -> assertTrue (dt.Year = 2024 && dt.Month = 10 && dt.Day = 2) "parseDateTimeOption: Valid ISO date"
    | None -> printfn "✗ FAIL: parseDateTimeOption: Valid ISO date"; testsFailed <- testsFailed + 1)

assertEqual (parseDateTimeOption "invalid-date") None "parseDateTimeOption: Invalid date"

// Test trim
assertEqual (trim "  hello  ") "hello" "trim: String with spaces"
assertEqual (trim "hello") "hello" "trim: String without spaces"
assertEqual (trim "   ") "" "trim: Only spaces"

// Test splitCsvLine
assertEqual (splitCsvLine "a,b,c") [|"a"; "b"; "c"|] "splitCsvLine: Simple CSV"
assertEqual (splitCsvLine " a , b , c ") [|"a"; "b"; "c"|] "splitCsvLine: CSV with spaces"

// ============================================================================
// Test Suite 2: Line to Record Conversion
// ============================================================================

printfn "\n========== TEST SUITE 2: LINE TO RECORD CONVERSION =========="

// Test lineToOrder - Valid case
let validOrderLine = "1,112,2024-10-02T03:05:39,Pending,P"
(match lineToOrder validOrderLine with
    | Some order -> 
        assertTrue (order.id = 1 && order.client_id = 112 && order.status = "Pending" && order.origin = "P") 
            "lineToOrder: Valid order line"
    | None -> printfn "✗ FAIL: lineToOrder: Valid order line"; testsFailed <- testsFailed + 1)

// Test lineToOrder - Invalid cases
assertEqual (lineToOrder "1,112,invalid-date,Pending,P") None "lineToOrder: Invalid date"
assertEqual (lineToOrder "abc,112,2024-10-02T03:05:39,Pending,P") None "lineToOrder: Invalid ID"
assertEqual (lineToOrder "1,2,3") None "lineToOrder: Too few fields"

// Test lineToOrderItem - Valid case
let validItemLine = "1,210,7,12.79,0.08"
(match lineToOrderItem validItemLine with
    | Some item ->
        assertTrue (item.order_id = 1 && item.product_id = 210 && item.quantity = 7.0 && item.price = 12.79 && item.tax = 0.08)
            "lineToOrderItem: Valid item line"
    | None -> printfn "✗ FAIL: lineToOrderItem: Valid item line"; testsFailed <- testsFailed + 1)

// Test lineToOrderItem - Invalid cases
assertEqual (lineToOrderItem "1,210,abc,12.79,0.08") None "lineToOrderItem: Invalid quantity"
assertEqual (lineToOrderItem "1,2,3") None "lineToOrderItem: Too few fields"

// ============================================================================
// Test Suite 3: Revenue and Tax Calculations
// ============================================================================

printfn "\n========== TEST SUITE 3: REVENUE AND TAX CALCULATIONS =========="

let testItem = { order_id = 1; product_id = 100; quantity = 5.0; price = 10.0; tax = 0.1 }

assertFloatEqual (calculateItemRevenue testItem) 50.0 0.01 "calculateItemRevenue: 5 * 10 = 50"

assertFloatEqual (calculateItemTax testItem) 5.0 0.01 "calculateItemTax: 50 * 0.1 = 5"

let testItem2 = { order_id = 1; product_id = 100; quantity = 2.5; price = 20.0; tax = 0.2 }
assertFloatEqual (calculateItemRevenue testItem2) 50.0 0.01 "calculateItemRevenue: 2.5 * 20 = 50"
assertFloatEqual (calculateItemTax testItem2) 10.0 0.01 "calculateItemTax: 50 * 0.2 = 10"

// ============================================================================
// Test Suite 4: Filtering Functions
// ============================================================================

printfn "\n========== TEST SUITE 4: FILTERING FUNCTIONS =========="

let order1 = { id = 1; client_id = 112; order_date = DateTime(2024, 10, 2); status = "Pending"; origin = "P" }
let order2 = { id = 2; client_id = 117; order_date = DateTime(2024, 8, 17); status = "Complete"; origin = "O" }
let order3 = { id = 3; client_id = 120; order_date = DateTime(2024, 9, 10); status = "Cancelled"; origin = "O" }

// Test with no filters
assertTrue (filterOrdersByStatusAndOrigin None None order1) "filterOrdersByStatusAndOrigin: No filters accepts all"
assertTrue (filterOrdersByStatusAndOrigin None None order2) "filterOrdersByStatusAndOrigin: No filters accepts all (2)"

// Test with status filter only
assertTrue (filterOrdersByStatusAndOrigin (Some "Pending") None order1) "filterOrdersByStatusAndOrigin: Status match"
assertTrue (not (filterOrdersByStatusAndOrigin (Some "Pending") None order2)) "filterOrdersByStatusAndOrigin: Status mismatch"

// Test with origin filter only
assertTrue (filterOrdersByStatusAndOrigin None (Some "P") order1) "filterOrdersByStatusAndOrigin: Origin match"
assertTrue (not (filterOrdersByStatusAndOrigin None (Some "P") order2)) "filterOrdersByStatusAndOrigin: Origin mismatch"

// Test with both filters
assertTrue (filterOrdersByStatusAndOrigin (Some "Complete") (Some "O") order2) "filterOrdersByStatusAndOrigin: Both filters match"
assertTrue (not (filterOrdersByStatusAndOrigin (Some "Complete") (Some "P") order2)) "filterOrdersByStatusAndOrigin: Both filters mismatch"

// Test case insensitivity
assertTrue (filterOrdersByStatusAndOrigin (Some "pending") None order1) "filterOrdersByStatusAndOrigin: Case insensitive status"
assertTrue (filterOrdersByStatusAndOrigin None (Some "p") order1) "filterOrdersByStatusAndOrigin: Case insensitive origin"

// ============================================================================
// Test Suite 5: Inner Join
// ============================================================================

printfn "\n========== TEST SUITE 5: INNER JOIN =========="

let orders = [order1; order2; order3]
let item1 = { order_id = 1; product_id = 100; quantity = 5.0; price = 10.0; tax = 0.1 }
let item2 = { order_id = 1; product_id = 101; quantity = 2.0; price = 20.0; tax = 0.15 }
let item3 = { order_id = 2; product_id = 102; quantity = 3.0; price = 15.0; tax = 0.2 }
let items = [item1; item2; item3]

let joinResult = innerJoinOrdersAndItems orders items

assertEqual joinResult.Length 3 "innerJoinOrdersAndItems: Correct number of joined records"

// Order 1 should have 2 items
let order1Items = joinResult |> List.filter (fun (o, _) -> o.id = 1)
assertEqual order1Items.Length 2 "innerJoinOrdersAndItems: Order 1 has 2 items"

// Order 2 should have 1 item
let order2Items = joinResult |> List.filter (fun (o, _) -> o.id = 2)
assertEqual order2Items.Length 1 "innerJoinOrdersAndItems: Order 2 has 1 item"

// Order 3 should have 0 items (no join)
let order3Items = joinResult |> List.filter (fun (o, _) -> o.id = 3)
assertEqual order3Items.Length 0 "innerJoinOrdersAndItems: Order 3 has 0 items"

// ============================================================================
// Test Suite 6: Aggregation
// ============================================================================

printfn "\n========== TEST SUITE 6: AGGREGATION =========="

let aggregationResult = aggregateOrderTotals joinResult

assertEqual aggregationResult.Length 2 "aggregateOrderTotals: Correct number of aggregated orders"

// Check Order 1 totals: (5*10 + 2*20) = 90, taxes = (50*0.1 + 40*0.15) = 11
let order1Summary = aggregationResult |> List.find (fun s -> s.order_id = 1)
assertFloatEqual order1Summary.total_amount 90.0 0.01 "aggregateOrderTotals: Order 1 total amount"
assertFloatEqual order1Summary.total_taxes 11.0 0.01 "aggregateOrderTotals: Order 1 total taxes"

// Check Order 2 totals: (3*15) = 45, taxes = (45*0.2) = 9
let order2Summary = aggregationResult |> List.find (fun s -> s.order_id = 2)
assertFloatEqual order2Summary.total_amount 45.0 0.01 "aggregateOrderTotals: Order 2 total amount"
assertFloatEqual order2Summary.total_taxes 9.0 0.01 "aggregateOrderTotals: Order 2 total taxes"

// ============================================================================
// Test Suite 7: Complete ETL Pipeline
// ============================================================================

printfn "\n========== TEST SUITE 7: COMPLETE ETL PIPELINE =========="

// Test without filters
let etlResult = processETL orders items None None
assertEqual etlResult.Length 2 "processETL: No filters processes all orders"
assertTrue (etlResult.[0].order_id = 1) "processETL: Results are sorted by order_id"

// Test with status filter
let etlResultFiltered = processETL orders items (Some "Complete") None
assertEqual etlResultFiltered.Length 1 "processETL: Status filter works"
assertEqual etlResultFiltered.[0].order_id 2 "processETL: Correct order selected by filter"

// Test with origin filter
let etlResultOriginFiltered = processETL orders items None (Some "P")
assertEqual etlResultOriginFiltered.Length 1 "processETL: Origin filter works"
assertEqual etlResultOriginFiltered.[0].order_id 1 "processETL: Correct order selected by origin filter"

// Test with both filters
let etlResultBothFiltered = processETL orders items (Some "Complete") (Some "O")
assertEqual etlResultBothFiltered.Length 1 "processETL: Both filters work together"

// ============================================================================
// Test Suite 8: Monthly Summaries
// ============================================================================

printfn "\n========== TEST SUITE 8: MONTHLY SUMMARIES =========="

let monthlySummaries = calculateMonthlySummaries orders aggregationResult

assertTrue (monthlySummaries.Length > 0) "calculateMonthlySummaries: Generates summaries"

// Check that summaries are sorted by year and month
let isSorted = 
    monthlySummaries
    |> List.pairwise
    |> List.forall (fun (a, b) -> (a.year, a.month) <= (b.year, b.month))
assertTrue isSorted "calculateMonthlySummaries: Results are sorted by year and month"

// Verify that order_count matches the number of orders in that month
let october2024 = monthlySummaries |> List.tryFind (fun m -> m.year = 2024 && m.month = 10)
(match october2024 with
| Some summary -> assertEqual summary.order_count 1 "calculateMonthlySummaries: Correct order count"
| None -> printfn "✗ FAIL: calculateMonthlySummaries: October 2024 not found"; testsFailed <- testsFailed + 1)

// ============================================================================
// Test Suite 9: Edge Cases
// ============================================================================

printfn "\n========== TEST SUITE 9: EDGE CASES =========="

// Empty lists
let emptyJoin = innerJoinOrdersAndItems [] items
assertEqual emptyJoin.Length 0 "innerJoinOrdersAndItems: Empty orders list"

let emptyJoin2 = innerJoinOrdersAndItems orders []
assertEqual emptyJoin2.Length 0 "innerJoinOrdersAndItems: Empty items list"

// Single item
let singleItemAgg = aggregateOrderTotals [(order1, item1)]
assertEqual singleItemAgg.Length 1 "aggregateOrderTotals: Single item"
assertFloatEqual singleItemAgg.[0].total_amount 50.0 0.01 "aggregateOrderTotals: Single item amount"

// Zero quantity
let zeroQuantityItem = { order_id = 1; product_id = 100; quantity = 0.0; price = 10.0; tax = 0.1 }
assertFloatEqual (calculateItemRevenue zeroQuantityItem) 0.0 0.01 "calculateItemRevenue: Zero quantity"
assertFloatEqual (calculateItemTax zeroQuantityItem) 0.0 0.01 "calculateItemTax: Zero quantity"

// Zero tax
let zeroTaxItem = { order_id = 1; product_id = 100; quantity = 5.0; price = 10.0; tax = 0.0 }
assertFloatEqual (calculateItemTax zeroTaxItem) 0.0 0.01 "calculateItemTax: Zero tax"

// ============================================================================
// Test Summary
// ============================================================================

printfn "\n========== TEST SUMMARY =========="
printfn "Tests Passed: %d" testsPassed
printfn "Tests Failed: %d" testsFailed
printfn "Total Tests: %d" (testsPassed + testsFailed)

if testsFailed = 0 then
    printfn "\n✓ ALL TESTS PASSED!"
else
    printfn "\n✗ SOME TESTS FAILED!"