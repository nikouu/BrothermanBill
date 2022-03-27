namespace BrothermanBill.Models
{
    public readonly record struct CommandDto(string Name, List<string> Aliases, string Summary);
}
