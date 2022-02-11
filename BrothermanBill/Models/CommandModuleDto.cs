namespace BrothermanBill.Models
{
    public class CommandModuleDto
    {
        public string Name { get; set; }

        public string Summary { get; set; }

        public List<CommandDto> Modules { get; set; }
    }
}
