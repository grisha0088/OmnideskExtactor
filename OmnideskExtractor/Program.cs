using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;
using OmnideskRestClient;


namespace Extractor
{
    internal class Program
    {
        public static void Main()
        {
            //этот код создаёт и конфигурирует службу, используется Topshelf
            HostFactory.Run(x =>
            {
                x.Service<Prog>(s =>
                {
                    s.ConstructUsing(name => new Prog()); //создаём службу из класса Prog
                    s.WhenStarted(tc => tc.Start()); //говорим, какой метод будет при старте службы
                    s.WhenStopped(tc => tc.Stop()); //говорим, какой метод выполнится при остановке службы
                });
                x.RunAsNetworkService(); //указываем свойства службы
                x.SetDescription("Service for Omnidesk. Extracts all tickets in database");
                x.SetDisplayName("OmnideskExtractor");
                x.SetServiceName("OmnideskExtractor");
                x.StartAutomaticallyDelayed();
            });
        }
    }

    internal class Prog
    {
        private Parametr omnideskParam; //адрес Omnidesk с которой работаем
        private Parametr userLoginParam; 
        private Parametr userPasswordParam; 
        private Parametr intervalParam; //как насколько делаем паузы при опросе Omnidesk (разрешено 500 запросов в час на активного сотрудника)
        OmnideskClient OmnideskClient;

        public void Start() //метод вызывается при старте службы
        {
            try
            {
                try //пишем в лог о запуске службы
                {
                    using (var repository = new Repository<DbContext>()) //использую репозиторий для работы с БД, какая будет БД указано в DbContext
                    {
                        var logReccord = new Log
                        {
                            Date = DateTime.Now,
                            MessageTipe = "info",
                            Operation = "StartService",
                            Exception = ""
                        };
                        repository.Create(logReccord);
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(60000);
                        // еcли не доступна БД и не получается залогировать запуск, ждём 60 секунд и пробуем еще раз.
                    using (var repository = new Repository<DbContext>())
                    {
                        repository.Create(new Log //создаю объект Log и пишу его в БД
                        {
                            Date = DateTime.Now,
                            MessageTipe = "error",
                            Operation = "StartService",
                            Exception = ex.GetType() + ": " + ex.Message
                        });
                        repository.Create(new Log
                        {
                            Date = DateTime.Now,
                            MessageTipe = "info",
                            Operation = "StartService2Attemp",
                            Exception = ""
                        });
                    }
                }

                using (var repository = new Repository<DbContext>()) //инициализирую парамтры приложения из БД
                {
                    omnideskParam = repository.Get<Parametr>(p => p.Name == "Omnidesk");  //ссылка на омнидеск
                    userLoginParam = repository.Get<Parametr>(p => p.Name == "dafaultuserlogin"); //логин
                    userPasswordParam = repository.Get<Parametr>(p => p.Name == "dafaultuserpassword"); //пароль
                    intervalParam = repository.Get<Parametr>(p => p.Name == "interval"); //пауза между запросами в омнидеск
                    OmnideskClient = new OmnideskClient(omnideskParam.Value, userLoginParam.Value, userPasswordParam.Value);
                }

                //создаю и запускаю задачу, чтобы в бесконечном цикле будет проверять Омнидеск
                Task tsk = new Task(CheckOmnidesk);
                tsk.Start();
            }
            catch (Exception ex)
            {
                using (var repository = new Repository<DbContext>())
                {
                    repository.Create(new Log
                    {
                        Date = DateTime.Now,
                        MessageTipe = "fatal",
                        Operation = "StartService",
                        Exception = ex.GetType() + ": " + ex.Message
                    });
                }
            }
        }

        public void Stop() //метод вызывается при остановке службы
        {
            try
            {
                using (var repository = new Repository<DbContext>())
                {
                    repository.Create(new Log
                    {
                        Date = DateTime.Now,
                        MessageTipe = "info",
                        Operation = "StopService",
                        Exception = "",
                    });
                }
                Thread.ResetAbort();
            }
            catch (Exception ex)
            {
                using (var repository = new Repository<DbContext>())
                {
                    repository.Create(new Log
                    {
                        Date = DateTime.Now,
                        MessageTipe = "fatal",
                        Operation = "StopService",
                        Exception = ex.GetType() + ": " + ex.Message
                    });
                }
            }
        }

        public void CheckOmnidesk()
        {
            while (true)
            {
                try
                {
                    ProcessLabels(GetLabels()); //получил и обработал метки
                    ProcessStaff(GetStaff()); //получил и обработал персонал
                    ProcessGroups(GetGroups()); //получил и обработал группы
                    ProcessCases(GetCases()); //получил и обработал заявки
                    using (var repository = new Repository<DbContext>()) //создаю репозиторий для работы с БД
                    {
                        repository.Create(new Log()
                        {
                            MessageTipe = "info",
                            Date = DateTime.Now,
                            AddInfo = "Синхронизация прошла успешно"
                        });
                    }
                    Thread.Sleep(1800000);  //синхронизация будет проходить раз в 30 минут
                }
                catch (Exception e)
                {
                    Thread.Sleep(300000);
                    using (var repository = new Repository<DbContext>()) //создаю репозиторий для работы с БД
                    {
                        repository.Create(new Log()
                        {
                            MessageTipe = "error",
                            Date = DateTime.Now,
                            Exception = e.Message,
                            AddInfo = "InnerException: " + e.InnerException.Message + System.Environment.NewLine 
                            + "StackTrace: " + e.StackTrace
                        });
                    }
                }
            }
        }

        void ProcessLabels(List<Label> labels)
        {
            using (var repository = new Repository<DbContext>()) 
            {
                foreach (Label l in labels)  
                {
                    var labelFromDB = repository.Get<Lable>(ls => ls.label_id == l.label_id);
                    if (labelFromDB == null)
                    {
                        repository.Create(new Lable()
                        {
                            label_id = l.label_id,
                            label_title = l.label_title
                        });
                    }
                }
            }
        }
        List<Label> GetLabels()
        {
            var result = new List<Label>();
            int labelPage = 0;
            while (true)
            {
                var labelsFromCurrentPage = OmnideskClient.GetLables(++labelPage, 100); //получаю по 100, начиная с 1 страницы
                Thread.Sleep(int.Parse(intervalParam.Value)); //пауза, чтобы не заспамить API

                if (labelsFromCurrentPage == null) break; //если null, значит прочитали всё

                result.AddRange(labelsFromCurrentPage); //добавляю к результату полученные метки
                if (labelsFromCurrentPage.Count < 100) break;  //если получили меньше 100 - значит прочитали все
            }
            return result;
        }

        void ProcessStaff(List<Staff> staff)
        {
            using (var repository = new Repository<DbContext>()) //создаю репозиторий для работы с БД
            {
                foreach (var st in staff)
                {
                    var staffFromDB = repository.Get<Assignee>(a => a.assignee_id == st.staff_id);
                    if (staffFromDB == null)
                    {
                        repository.Create(new Assignee()
                        {
                            assignee_id = st.staff_id,
                            assignee_email = st.staff_email,
                            assignee_full_name = st.staff_full_name,
                            active = st.active,
                            created_at = st.created_at,
                            updated_at = st.updated_at
                        });
                    }
                }
            }
        }
        List<Staff> GetStaff()
        {
            var result = new List<Staff>();
            int labelPage = 0;
            while (true)
            {
                var StaffFromCurrentPage = OmnideskClient.GetStaff(++labelPage, 100); //получаю по 100, начиная с 1 страницы
                Thread.Sleep(int.Parse(intervalParam.Value)); //пауза, чтобы не заспамить API

                if (StaffFromCurrentPage == null) break; //если null, значит прочитали всё

                result.AddRange(StaffFromCurrentPage); //добавляю к результату полученные заявки
                if (StaffFromCurrentPage.Count < 100) break;  //если получили меньше 100 заявок - значит прочитали все
            }
            return result;
        }

        void ProcessGroups(List<Group> groups)
        {
            using (var repository = new Repository<DbContext>()) //создаю репозиторий для работы с БД
            {
                foreach (var group in groups)
                {
                    var groupFromDB = repository.Get<IssueGroup>(g => g.group_id == group.group_id);
                    if (groupFromDB == null) //если такой группы нет - создаём
                    {
                        repository.Create(new IssueGroup()
                        {
                            group_id = group.group_id,
                            group_title = group.group_title,
                            group_from_name = group.group_from_name,
                            group_signature = group.group_signature,
                            active = group.active,
                            created_at = group.created_at,
                            updated_at = group.updated_at
                        });
                        continue;
                    }
                    if (groupFromDB.updated_at != group.updated_at) //если аптайм отличается - обновляем
                    {
                        groupFromDB.group_title = group.group_title;
                        groupFromDB.group_from_name = group.group_from_name;
                        groupFromDB.group_signature = group.group_signature;
                        groupFromDB.active = group.active;
                        groupFromDB.created_at = group.created_at;
                        groupFromDB.updated_at = group.updated_at;
                        repository.Update();
                    }
                }
            }
        }
        List<Group> GetGroups()
        {
            var result = new List<Group>();
            int Page = 0;
            while (true)
            {
                var GroupsFromCurrentPage = OmnideskClient.GetGroups(++Page, 100); //получаю по 100, начиная с 1 страницы
                Thread.Sleep(int.Parse(intervalParam.Value)); //пауза, чтобы не заспамить API

                if (GroupsFromCurrentPage == null) break; //если null, значит прочитали всё

                result.AddRange(GroupsFromCurrentPage); //добавляю к результату полученные объекты
                if (GroupsFromCurrentPage.Count < 100) break;  //если получили меньше 100  - значит прочитали все
            }
            return result;
        }

        void ProcessCases(List<Case> cases)
        {
            using (var repository = new Repository<DbContext>()) //создаю репозиторий для работы с БД
            {
                int ii = 0;
                foreach (Case c in cases)
                {
                    ii++;
                    var lbls = new List<Lable>(); //далаем метки тикета как объект
                    foreach (var l in c.labels)
                    {
                        var label = repository.Get<Lable>(lbl => lbl.label_id == l);
                        if (label == null) return; //если в справочнике нет такого значения - выходим из метода
                        lbls.Add(repository.Get<Lable>(lbl => lbl.label_id == l));
                    }
                    var assignee = repository.Get<Assignee>(a => a.assignee_id == c.staff_id);
                    if (assignee == null) return;

                    var group = repository.Get<IssueGroup>(g => g.group_id == c.group_id);
                    if (group == null) return;

                    var priority = repository.Get<Priority>(p => p.name == c.priority);
                    if (priority == null)
                    {
                        repository.Create(new Priority() {name = c.priority});
                        priority = repository.Get<Priority>(p => p.name == c.priority);
                    }

                    var status = repository.Get<Status>(s => s.name == c.status);
                    if (status == null)
                    {
                        repository.Create(new Status() {name = c.status});
                        status = repository.Get<Status>(s => s.name == c.status);
                    }

                    bool? isEstimated = null;
                    string version = null;
                    string type = null;
                    string project = null;
                    if (c.custom_fields != null)
                    {

                        isEstimated = c.custom_fields.cf_198;
                        version = c.custom_fields.cf_321;
                        type = c.custom_fields.cf_322;
                        project = c.custom_fields.cf_381;
                    }

                    var ticketFromDB = repository.Get<Issue>(i => i.case_id == c.case_id);
                    if (ticketFromDB == null)
                    {
                        repository.Create(new Issue()
                        {
                            case_id = c.case_id,
                            case_number = c.case_number,
                            subject = c.subject,
                            user_id = c.user_id,
                            assignee = assignee,
                            group = group,
                            status = status,
                            priority = priority,
                            recipient = c.recipient,
                            deleted = c.deleted,
                            spam = c.spam,
                            created_at = c.created_at,
                            updated_at = c.updated_at,
                            isEstimated = isEstimated,
                            version = version,
                            type = type,
                            project = project,
                            Label = lbls
                        });
                    }
                    else
                    {
                        if (ticketFromDB.updated_at >= c.updated_at) return;
                        else
                        {
                            ticketFromDB.case_id = c.case_id;
                            ticketFromDB.case_number = c.case_number;
                            ticketFromDB.subject = c.subject;
                            ticketFromDB.user_id = c.user_id;
                            ticketFromDB.assignee = assignee;
                            ticketFromDB.group = group;
                            ticketFromDB.status = status;
                            ticketFromDB.priority = priority;
                            ticketFromDB.recipient = c.recipient;
                            ticketFromDB.deleted = c.deleted;
                            ticketFromDB.spam = c.spam;
                            ticketFromDB.created_at = c.created_at;
                            ticketFromDB.updated_at = c.updated_at;
                            ticketFromDB.isEstimated = isEstimated;
                            ticketFromDB.version = version;
                            ticketFromDB.type = type;
                            ticketFromDB.project = project;
                            ticketFromDB.Label = lbls;
                            repository.Update();
                        }
                    }
                }
            }
        }
        List<Case> GetCases()
       {
            using (var repository = new Repository<DbContext>()) //создаю репозиторий для работы с БД
            {
                // вычитываю дату изменения последнего тикета из БД
                var lastProcessedCase = repository.GetList<Issue>().OrderByDescending(p => p.updated_at).FirstOrDefault();
                var lastProcessedTime = lastProcessedCase != null ? lastProcessedCase.updated_at : new DateTime(1900, 1, 1);

                var result = new List<Case>(); 
                int casePage = 0;
                while (true)
                {
                    var casesFromCurrentPage = OmnideskClient.GetCases(++casePage, 100); //получаю по 100 заявки, начиная с 1 страницы
                    Thread.Sleep(int.Parse(intervalParam.Value)); //пауза, чтобы не заспамить API
                    
                    if(casesFromCurrentPage == null) break; //если null, значит прочитали всё

                    result.AddRange(casesFromCurrentPage); //добавляю к результату полученные заявки
                    //if (casePage > 10) break; //для теста
                    if (casesFromCurrentPage.Count < 100) break;  //если получили меньше 100 заявок - значит прочитали все
                    if (casesFromCurrentPage.Last().updated_at < lastProcessedTime) break; //проверяем, нет ли в БД заявок свежее, чем мы получили
                }
                return result;
            }
        } 
    }
}