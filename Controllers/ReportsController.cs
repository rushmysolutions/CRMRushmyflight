using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Helpers;
using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.Services;

namespace RushMyBookings.Crm.Controllers
{
    public class ReportsController(ICrmDataService dataService) : CrmControllerBase(dataService)
    {
        private int? ScopeUserId() =>
            !CrmAccess.IsRestricted(User)
                ? null
                : int.TryParse(CrmAccess.UserId(User), out var id) ? id : -1;

        [HttpGet("~/Reports/Bookings")]
        public async Task<IActionResult> Bookings([FromQuery] BookingReportFilter filter)
        {
            var scope = ScopeUserId();
            var (rows, total) = await DataService.GetBookingReportAsync(filter, scope);

            return View(new BookingReportViewModel
            {
                Filter = filter,
                Options = await DataService.GetBookingReportOptionsAsync(),
                Rows = rows,
                TotalCount = total,
                IsAgent = scope.HasValue
            });
        }

        [HttpGet("~/Reports/Bookings/Excel")]
        public async Task<IActionResult> BookingsExcel([FromQuery] BookingReportFilter filter)
        {
            var (rows, passengers) = await DataService.GetBookingReportExportAsync(filter);
            var bytes = BookingReportExcel.Build(rows, passengers);

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"BookingReport_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }

        [HttpGet("~/Reports/Bookings/Txt")]
        public IActionResult BookingsTxt([FromQuery] BookingReportFilter filter)
        {
            TempData["ReportInfo"] = "TXT download is coming in the next step.";
            return RedirectToAction(nameof(Bookings), filter);
        }

        

        [HttpGet("~/Reports/Bookings/Txt/{id:int}")]
        public async Task<IActionResult> BookingTxt(int id)
        {
            var detail = await DataService.GetBookingByIdAsync(id);
            var edit = await DataService.GetBookingForEditAsync(id);
            if (detail is null || edit is null) return NotFound();

            var text = BookingTxtBuilder.Build(detail, edit);
            var bytes = new System.Text.UTF8Encoding(false).GetBytes(text);
            var fileName = $"{(string.IsNullOrWhiteSpace(detail.ReferenceNo) ? $"booking-{id}" : detail.ReferenceNo.Trim())}.txt";

            return File(bytes, "text/plain; charset=utf-8", fileName);
        }
    }
}
