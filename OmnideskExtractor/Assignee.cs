using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extractor
{
    public class Assignee
    {
        public int id { get; set; }
        public int assignee_id { get; set; }
        public string assignee_email { get; set; }
        public string assignee_full_name { get; set; }
        public bool active { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
    }
}
