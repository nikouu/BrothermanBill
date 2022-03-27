namespace BrothermanBill.Models
{
    public readonly record struct CommandModuleDto(string Name, string Summary, List<CommandDto> Modules);
}
