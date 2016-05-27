using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmnideskRestClient;

namespace Extractor
{
    public class Issue
    {
        public int id { get; set; }
        public int case_id { get; set; }
        public string case_number { get; set; }
        public string subject { get; set; }
        public int user_id { get; set; }
        public Assignee assignee { get; set; }
        public IssueGroup group { get; set; }
        public Status status { get; set; }
        public Priority priority { get; set; }
        public string recipient { get; set; }
        public bool deleted { get; set; }
        public bool spam { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public bool? isEstimated { get; set; }   //оценить ответ (да/нет)
        public string version { get; set; } //версия приложения
        public string type { get; set; } //тип обращения
        public string project { get; set; } //проект
        public List<Lable> Label { get; set; }
    }
}
