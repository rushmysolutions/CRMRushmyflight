using Microsoft.AspNetCore.Mvc;
using RushMyBookings.Crm.Models;
using RushMyBookings.Crm.Services;
using RushMyBookings.Crm.ViewModels;
using System.Net;
using System.Net.Sockets;

namespace RushMyBookings.Crm.Controllers;

public class AgentsController(ICrmDataService dataService) : CrmControllerBase(dataService)
{
    private int? ScopeUserId() =>
    !CrmAccess.IsRestricted(User)
        ? null
        : int.TryParse(CrmAccess.UserId(User), out var id) ? id : -1;


    private bool IsAgent => CrmAccess.IsRestricted(User);
    private bool IsSelf(int id) => CrmAccess.UserId(User) == id.ToString();
    
    public async Task<IActionResult> Index(string? name, string? phone, string? email, int page = 1)
    {
        const int pageSize = 20;
        page = Math.Max(1, page);

        var (agents, total) = await DataService.SearchAgentsAsync(
            name, phone, email, page, pageSize, ScopeUserId());

        return View(new AgentListViewModel
        {
            Agents = agents,
            Total = total,
            Page = page,
            PageSize = pageSize,
            Name = name,
            Phone = phone,
            Email = email
        });
    }
   

    public async Task<IActionResult> AgentManagement(DateTime? fromDate, DateTime? toDate)
    {
        var agents = await DataService.GetAgentPerformanceAsync(fromDate, toDate, ScopeUserId());

        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        return View(agents);
    }


    [HttpGet("~/Agents/AgentForm/{id:int?}")]
    public async Task<IActionResult> AgentForm(int? id)
    {
        if (id is null) return View(new AgentFormViewModel());

        var model = await DataService.GetAgentFormAsync(id.Value);
        return model is null ? NotFound() : View(model);
    }




    [HttpGet("~/Agents/Details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        if (IsAgent && !IsSelf(id)) return Forbid();
        var model = await DataService.GetAgentDetailsAsync(id);
        return model is null ? NotFound() : View(model);
    }




    [HttpPost("~/Agents/ToggleStatus/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, string? name, string? phone, string? email, int page = 1)
    {
        await DataService.ToggleAgentStatusAsync(id);
        return RedirectToAction(nameof(Index), new { name, phone, email, page });
    }

    [HttpGet("~/Agents/ChangePassword/{id:int}")]
    public async Task<IActionResult> ChangePassword(int id)
    {
        var username = await DataService.GetAgentUsernameAsync(id);
        if (username is null) return NotFound();

        return View(new ChangePasswordSaleViewModel { UserId = id, Username = username });
    }

    [HttpPost("~/Agents/ChangePassword/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(int id, ChangePasswordSaleViewModel model)
    {
        model.UserId = id;

        if (!ModelState.IsValid)
        {
            model.Username = await DataService.GetAgentUsernameAsync(id) ?? "";
            return View(model);
        }

        if (!await DataService.ChangeAgentPasswordAsync(id, model.NewPassword))
            return NotFound();

        TempData["Success"] = $"Password updated for {model.Username}.";
        return RedirectToAction(nameof(Index));
    }


    [HttpPost("~/Agents/AgentForm/{id:int?}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AgentForm(int? id, AgentFormViewModel model)
    {
        if (id is > 0) model.UserId = id.Value;

        if (!ModelState.IsValid)
            return View(model);

        var result = await DataService.SaveAgentAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(result.ErrorField ?? string.Empty, result.Error ?? "Could not save agent.");
            return View(model);
        }

        TempData["Success"] = result.Created
            ? $"Agent {model.Username} created. Password: {result.GeneratedPassword}"
            : $"Agent {model.Username} updated.";

        return RedirectToAction(nameof(Index));
    }


    
    private static (string Normalized, List<string> Invalid) NormalizeIps(string? raw)
    {
        var invalid = new List<string>();
        var valid = new List<string>();

        var parts = (raw ?? "").Split(new[] { ',', ' ', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var p in parts)
        {
            if (p.Count(c => c == '.') == 3
                && IPAddress.TryParse(p, out var ip)
                && ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var s = ip.ToString();
                if (!valid.Contains(s)) valid.Add(s);
            }
            else invalid.Add(p);
        }

        return (string.Join(",", valid), invalid);
    }


    [HttpGet]
    public async Task<IActionResult> ManageIp(int id)
    {
        if (ScopeUserId().HasValue) return Forbid();

        var model = await DataService.GetAgentIpsAsync(id);
        if (model is null) return NotFound();

        ViewBag.CurrentIp = CurrentClientIp();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManageIp(ManageIpViewModel model)
    {
        if (ScopeUserId().HasValue) return Forbid();
        model.IsAllUsers = false;

        if (!ValidateAllowedIps(model, out var normalized))
        {
            var existing = await DataService.GetAgentIpsAsync(model.UserId);
            if (existing is null) return NotFound();
            model.Username = existing.Username;
            return View(model);
        }

        if (!await DataService.UpdateAgentIpsAsync(model.UserId, normalized))
            return NotFound();

        TempData["Success"] = $"Allowed IPs updated for {model.Username}.";
        return RedirectToAction(nameof(ManageIp), new { id = model.UserId });
    }

    // ---------- All agents ----------
    [HttpGet]
    public IActionResult UpdateAllIps()
    {
        if (ScopeUserId().HasValue) return Forbid();

        return View("ManageIp", new ManageIpViewModel { IsAllUsers = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAllIps(ManageIpViewModel model)
    {
        if (ScopeUserId().HasValue) return Forbid();
        model.IsAllUsers = true;

        if (!ValidateAllowedIps(model, out var normalized))
            return View("ManageIp", model);

        var count = await DataService.UpdateAllAgentIpsAsync(normalized);

        TempData["Success"] = $"Allowed IPs updated for all users ({count} agents).";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Shared validation ----------
    private bool ValidateAllowedIps(ManageIpViewModel model, out string normalized)
    {
        var (norm, invalid) = NormalizeIps(model.AllowedIps);
        normalized = norm;

        if (invalid.Count > 0)
            ModelState.AddModelError(nameof(model.AllowedIps), $"Invalid IP(s): {string.Join(", ", invalid)}");
        else if (norm.Length == 0)
            ModelState.AddModelError(nameof(model.AllowedIps), "Enter at least one IP address");
        else if (norm.Length > 250)
            ModelState.AddModelError(nameof(model.AllowedIps), "Too many IPs — max 250 characters");

        return ModelState.IsValid;
    }

    private string? CurrentClientIp()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip is null) return null;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? ip.ToString() : null;
    }
}
