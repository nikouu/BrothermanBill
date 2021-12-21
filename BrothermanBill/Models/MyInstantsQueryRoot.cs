using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill.Models
{
    public class MyInstantsQueryRoot
    {
        public int Count { get; set; }
        public string Next { get; set; }
        public object Previous { get; set; }
        public MyInstantsQueryResult[] Results { get; set; }

    }
}
