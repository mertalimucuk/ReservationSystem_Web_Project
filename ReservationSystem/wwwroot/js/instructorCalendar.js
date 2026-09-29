document.addEventListener("DOMContentLoaded", function () {
  const calendarEl = document.getElementById("instructorCalendar");
  if (!calendarEl) {
    console.error(" Takvim elemanı bulunamadı: #instructorCalendar");
    return;
  }

  const calendar = new FullCalendar.Calendar(calendarEl, {
    themeSystem: "bootstrap5",
    initialView: "dayGridMonth",
    height: 650,
    headerToolbar: {
      left: "prev,next today",
      center: "title",
      right: "dayGridMonth",
    },
    events: function (info, successCallback, failureCallback) {
      const year = info.start.getFullYear();
      const month = info.start.getMonth() + 1;

      console.log("Fetching:", year, month);

      fetch(
        `/InstructorPanel?handler=MonthlyReservations&year=${year}&month=${month}`
      )
        .then((response) => {
          if (!response.ok) {
            throw new Error(`Sunucu hatası: ${response.status}`);
          }
          return response.json();
        })
        .then((data) => {
          console.log("📥 Takvim verisi geldi:", data);
          successCallback(data);
        })
        .catch((error) => {
          console.error("Takvim verisi alınamadı:", error);
          failureCallback(error);
        });
    },
  });

  calendar.render();
  window.calendar = calendar;
});
