using System.Data.Entity;
using OmnideskRestClient;

namespace Extractor
{
    public class DbContext : System.Data.Entity.DbContext
    {
        public DbContext(): base("DbConnection"){ }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Parametr> Parametrs { get; set; }
        public DbSet<Issue> Cases { get; set; }
        public DbSet<Lable> Lables { get; set; }
        public DbSet<Assignee> Assignees { get; set; }
        public DbSet<IssueGroup> Groups { get; set; }
        public DbSet<Priority> Priorities { get; set; }
        public DbSet<Status> Statuses { get; set; }
    }
}
