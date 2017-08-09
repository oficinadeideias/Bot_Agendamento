using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.Luis;
using Newtonsoft.Json;
using Microsoft.Bot.Builder.Luis.Models;


using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



namespace Microsoft.Bot.Sample.PizzaBot
{
    [LuisModel("7367d0a4-a3bc-4aa1-bb2b-381ef60189ab", "ffdf51c9b97741b694e5746bbf27e2f0", LuisApiVersion.V2)]
    [Serializable]
    class PizzaOrderDialog : LuisDialog<PizzaOrder>
    {
        private readonly BuildFormDelegate<PizzaOrder> MakePizzaForm;
        public string assuntoAgendamento;

        internal PizzaOrderDialog(BuildFormDelegate<PizzaOrder> makePizzaForm)
        {
            this.MakePizzaForm = makePizzaForm;
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("I'm sorry. I didn't understand you.");
            context.Wait(MessageReceived);
        }

        [LuisIntent("OrderPizza")]
        [LuisIntent("Agendamento")]
        public async Task ProcessPizzaForm(IDialogContext context, LuisResult result)
        {
            var entities = new List<EntityRecommendation>(result.Entities);
            if (!entities.Any((entity) => entity.Type == "Kind"))
            {
                // Infer kind
                foreach (var entity in result.Entities)
                {
                    string kind = null;
                    string assunto = null;

                    switch (entity.Type)
                    {
                        case "especializacao": assunto = entity.Entity;
                            assuntoAgendamento = entity.Entity;
                                break;
                    }
                    if (kind != null)
                    {
                        entities.Add(new EntityRecommendation(type: "Kind") { Entity = kind });
                        break;
                    }
                }
            }

            var pizzaForm = new FormDialog<PizzaOrder>(new PizzaOrder(), this.MakePizzaForm, FormOptions.PromptInStart, entities);
            context.Call<PizzaOrder>(pizzaForm, PizzaFormComplete);
        }

        private async Task PizzaFormComplete(IDialogContext context, IAwaitable<PizzaOrder> result)
        {
            PizzaOrder order = null;
            try
            {
                order = await result;
            }
            catch (OperationCanceledException)
            {
                await context.PostAsync("You canceled the form!");
                return;
            }

            if (order != null)
            {
                await context.PostAsync("Agendamento:  " + order.dia.ToString() + "  " + order.horario.ToString());
                UserCredential credential;
                string[] Scopes = { CalendarService.Scope.Calendar };
                string credPath = System.Environment.GetFolderPath(
                   System.Environment.SpecialFolder.Personal);

                using (var stream =
               new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                   GoogleClientSecrets.Load(stream).Secrets,
                   Scopes,
                   "user",
                   CancellationToken.None,
                   new FileDataStore(credPath, true)).Result;
                string ApplicationName = "Google Calendar API .NET Quickstart";

                var service = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });


                Event newEvent = new Event()
                {
                    //Summary = assuntoAgendamento,
                    Summary = "Cabeleleiro",
                    Location = order.Address,
                    Description = "blablalbalb",
                    Start = new EventDateTime()
                    {
                        DateTime = order.dia.AddHours(1),
                        TimeZone = "America/Los_Angeles",
                    },
                    End = new EventDateTime()
                    {
                        //DateTime = DateTime.Parse("2017-07-20T17:00:00-07:00"),
                        DateTime = order.dia.AddHours(2),
                        TimeZone = "America/Los_Angeles",
                    },
                    Recurrence = new String[] { "RRULE:FREQ=DAILY;COUNT=2" },
                    Attendees = new EventAttendee[] {
                    new EventAttendee() { Email = "thiagograciadio@gmail.com" },
                    new EventAttendee() { Email = "guilherme.yoshimura@itau-unibanco.com.br" },
                },
                    Reminders = new Event.RemindersData()
                    {
                        UseDefault = false,
                        Overrides = new EventReminder[] {
                        new EventReminder() { Method = "email", Minutes = 24 * 60 },
                        new EventReminder() { Method = "sms", Minutes = 10 },
                    }
                    }
                };
                String calendarId = "primary";
                EventsResource.InsertRequest request1 = service.Events.Insert(newEvent, calendarId);
                Event createdEvent = request1.Execute();
                Console.WriteLine("Event created: {0}", createdEvent.HtmlLink);

                // Define parameters of request.
                EventsResource.ListRequest request = service.Events.List("primary");
                request.TimeMin = DateTime.Now;
                request.ShowDeleted = false;
                request.SingleEvents = true;
                request.MaxResults = 10;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

                // List events.
                Events events = request.Execute();
                Console.WriteLine("Upcoming events:");
                if (events.Items != null && events.Items.Count > 0)
                {
                    foreach (var eventItem in events.Items)
                    {
                        string when = eventItem.Start.DateTime.ToString();
                        if (String.IsNullOrEmpty(when))
                        {
                            when = eventItem.Start.Date;
                        }
                        Console.WriteLine("{0} ({1})", eventItem.Summary, when);
                    }
                }
                else
                {
                    Console.WriteLine("No upcoming events found.");
                }
                Console.Read();

            }
        }
   
        
        enum Days { Saturday, Sunday, Monday, Tuesday, Wednesday, Thursday, Friday };

        [LuisIntent("StoreHours")]
        public async Task ProcessStoreHours(IDialogContext context, LuisResult result)
        {
            // Figuring out if the action is triggered or not
            var bestIntent = BestIntentFrom(result);
            var action = bestIntent.Actions.FirstOrDefault(t => t.Triggered.HasValue && t.Triggered.Value);
            if (action != null)
            {
                // extracting day parameter value from action parameters
                var dayParam = action.Parameters.Where(t => t.Name == "day").Select(t=> t.Value.FirstOrDefault(e => e.Type == "Day")?.Entity).First();
                Days day;
                if (Enum.TryParse(dayParam, true, out day))
                {
                    await this.StoreHoursResult(context, Awaitable.FromItem(day));
                    return;
                }
            }

            var days = (IEnumerable<Days>)Enum.GetValues(typeof(Days));
            PromptDialog.Choice(context, StoreHoursResult, days, "Which day of the week?",
                descriptions: from day in days
                              select (day == Days.Saturday || day == Days.Sunday) ? day.ToString() + "(no holidays)" : day.ToString());
        }

        private async Task StoreHoursResult(IDialogContext context, IAwaitable<Days> day)
        {
            var hours = string.Empty;
            switch (await day)
            {
                case Days.Saturday:
                case Days.Sunday:
                    hours = "5pm to 11pm";
                    break;
                default:
                    hours = "11am to 10pm";
                    break;
            }

            var text = $"Store hours are {hours}";
            await context.PostAsync(text);

            context.Wait(MessageReceived);
        }
    }
}
