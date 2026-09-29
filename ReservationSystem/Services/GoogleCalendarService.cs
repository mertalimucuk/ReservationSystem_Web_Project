using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

namespace ReservationSystem.Services
{
    public class GoogleCalendarService
    {
        private readonly string _jsonPath;
        private readonly string _calendarId = "tr.turkish#holiday@group.v.calendar.google.com"; // 🇹🇷 Tatil takvimi ID'si

        public GoogleCalendarService(string jsonPath)
        {
            _jsonPath = jsonPath;
        }

        public async Task<IList<Event>> GetHolidaysAsync(DateTime start, DateTime end)
        {
            Console.WriteLine($" Loading Google Calendar API credentials from: {_jsonPath}");

            using var stream = new FileStream(_jsonPath, FileMode.Open, FileAccess.Read);
            var credential = GoogleCredential.FromStream(stream)
                .CreateScoped(CalendarService.Scope.CalendarReadonly);

            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Reservation System"
            });

            var request = service.Events.List(_calendarId);
            request.TimeMin = start.ToUniversalTime();                 // UTC şart
            request.TimeMax = end.AddDays(1).ToUniversalTime();        //  Gün sonuna kadar
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            try
            {
                var response = await request.ExecuteAsync();

                if (response.Items != null)
                {
                    Console.WriteLine("=== Tüm Resmi Tatiller ===");
                    foreach (var item in response.Items)
                    {
                        var date = item.Start?.Date ?? item.Start?.DateTime?.ToString();
                        Console.WriteLine($"Public Holiday: {item.Summary} on {date}");
                    }
                }

                return response.Items ?? new List<Event>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Google Calendar API error: {ex.Message}");
                return new List<Event>();
            }
        }

        // Opsiyonel test fonksiyonu
        public async Task LogAll2025HolidaysAsync()
        {
            var holidays = await GetHolidaysAsync(
                new DateTime(2025, 1, 1),
                new DateTime(2025, 12, 31)
            );

            Console.WriteLine(" 2025 Tüm Tatiller:");
            foreach (var h in holidays)
            {
                Console.WriteLine($" {h.Summary} → {h.Start?.Date}");
            }
        }
    }
}
