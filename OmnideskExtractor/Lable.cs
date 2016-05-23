using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extractor
{
    public class Lable
    {
        public int id { get; set; }
        public int label_id { get; set; }
        public string label_title { get; set; }
        public List<Issue> Case { get; set; }
    }
}
