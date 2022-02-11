namespace BrothermanBill.Models
{
    public class CommandDto
    {
        public string Name { get; set; }

        public List<string> Aliases { get; set; }

        public string Summary { get; set; }
    }
}
