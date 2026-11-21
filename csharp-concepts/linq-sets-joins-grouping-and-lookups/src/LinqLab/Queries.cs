namespace LinqLab;

public static class Queries
{
    public static List<(string Customer, decimal Total)> InnerJoin(
        IEnumerable<Customer> customers, IEnumerable<Order> orders)
    {
        // TODO-00: One row per matching (customer, order) pair, using Join. Customers without
        //   orders and orders without a customer do not appear. Hint: Customer.Id is an int but
        //   Order.CustomerId is an int?, the two key selectors must have the same type: cast one.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static List<(string Customer, decimal? Total)> LeftJoin(
        IEnumerable<Customer> customers, IEnumerable<Order> orders)
    {
        // TODO-01: Like InnerJoin, but customers without orders appear once with a null total.
        //   Use the LeftJoin operator that arrived in .NET 10 (customers is the left side;
        //   the result selector receives a null order when there is no match).
        throw new NotImplementedException("Not implemented yet.");
    }

    public static List<(string Customer, int OrderCount)> CountOrders(
        IEnumerable<Customer> customers, IEnumerable<Order> orders)
    {
        // TODO-02: One row per customer with the number of orders, zero included, using GroupJoin.
        //   A GroupJoin never drops a left element.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static ILookup<int?, Order> OrdersByCustomer(IEnumerable<Order> orders)
    {
        // TODO-03: Build a lookup keyed by CustomerId with ToLookup. Unlike GroupBy, ToLookup runs
        //   now: later changes to the source list must not show up in the lookup, and asking for
        //   a missing key returns an empty sequence instead of throwing.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static List<Customer> NewCustomers(IEnumerable<Customer> known, IEnumerable<Customer> incoming)
    {
        // TODO-04: The incoming customers whose Email is not in `known`, compared ignoring case,
        //   with no duplicate emails in the result. Use ExceptBy (it is a set operation, so it
        //   already removes duplicates) instead of building a HashSet by hand.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static List<(int CustomerId, decimal Total)> TotalPerCustomer(IEnumerable<Order> orders)
    {
        // TODO-05: Sum the totals per customer with GroupBy, skipping orders without a customer,
        //   sorted by customer id. Remember that GroupBy is deferred: return a materialized list.
        throw new NotImplementedException("Not implemented yet.");
    }

#if NET11_0_OR_GREATER
    public static List<(string? Customer, int? OrderId)> ReconcileOrders(
        IEnumerable<Customer> customers, IEnumerable<Order> orders)
    {
        // TODO-08 (optional, needs the .NET 11 SDK): One row per matched (customer, order) pair,
        //   plus a row for every customer without orders and a row for every order without a
        //   customer, so nothing is dropped on either side. Use the .NET 11 FullJoin operator
        //   with a result selector; the side that has no match arrives as null. Then check
        //   where the unmatched orders land in the output order.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static List<(Customer? Known, Customer? Incoming)> ReconcileByEmail(
        IEnumerable<Customer> known, IEnumerable<Customer> incoming)
    {
        // TODO-08 (optional, part 2): The same idea with the overload that has no result
        //   selector and returns (TOuter, TInner) tuples, matching on Email ignoring case. A
        //   known customer that is not incoming comes out as (known, null) and a new incoming
        //   customer as (null, incoming).
        throw new NotImplementedException("Not implemented yet.");
    }
#endif
}
