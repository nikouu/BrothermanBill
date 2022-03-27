namespace BrothermanBill.Models
{
    public readonly record struct MyInstantsQueryRoot(int Count, string Next, object Previous, MyInstantsQueryResult[] Results);
}
