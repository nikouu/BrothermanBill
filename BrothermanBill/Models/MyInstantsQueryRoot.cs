namespace BrothermanBill.Models
{
    public readonly record struct MyInstantsQueryRoot(int Count, string? Next, string? Previous, MyInstantsQueryResult[]? Results);
}
