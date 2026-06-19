using KEYREGISTERAUTOMATION.Data;
using KEYREGISTERAUTOMATION.Models;
using KEYREGISTERAUTOMATION.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

[Authorize]
public class BaseController : Controller
{
    protected readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public BaseController(ApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    protected async Task<VwStaff?> GetCurrentUserAsync()
    {
        var windowsName = User.Identity?.Name;

        if (string.IsNullOrEmpty(windowsName))
            return null;

        var igg = windowsName.Split('\\').Last().ToUpper();

        return await _context.vwstaff
            .FirstOrDefaultAsync(s => s.IGG.ToUpper() == igg);
    }

    protected async Task RecalculateParentKeyAvailabilityAsync(int parentKeyId)
    {
        var parentKey = await _context.AllKeys.FirstOrDefaultAsync(k => k.Id == parentKeyId);
        if (parentKey == null) return;

        var totalKeys = await _context.IndividualKeys.CountAsync(k => k.ParentKeyId == parentKeyId);
        var availableKeys = await _context.IndividualKeys.CountAsync(k =>
            k.ParentKeyId == parentKeyId &&
            (k.Status == "Free" || string.IsNullOrWhiteSpace(k.Status)));

        parentKey.TotalNoofKeys = totalKeys;
        parentKey.NoofKeysAvaialble = availableKeys;

        await _context.SaveChangesAsync();
    }

    protected async Task SyncAssignmentStatusesAsync()
    {
        var today = DateTime.Today;

        var activeAssignments = await _context.AssignmentRecords
            .Where(a => a.Status != "Returned" && a.DueDate.HasValue)
            .ToListAsync();

        var hasChanges = false;
        var affectedParentKeyIds = new HashSet<int>();
        var overdueAssignmentsToNotify = new List<AssignmentRecord>();
        var upcomingAssignmentsToNotify = new List<AssignmentRecord>();

        foreach (var assignment in activeAssignments)
        {
            var previousStatus = assignment.Status ?? "";
            var newStatus = assignment.DueDate!.Value.Date < today ? "Overdue" : "Issued";

            var becameOverdue =
                !string.Equals(previousStatus, "Overdue", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(newStatus, "Overdue", StringComparison.OrdinalIgnoreCase);

            if (!string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                assignment.Status = newStatus;
                hasChanges = true;
            }

            if (assignment.IndividualKeyId.HasValue)
            {
                var individualKey = await _context.IndividualKeys
                    .FirstOrDefaultAsync(k => k.Id == assignment.IndividualKeyId.Value);

                if (individualKey != null &&
                    !string.Equals(individualKey.Status, newStatus, StringComparison.OrdinalIgnoreCase))
                {
                    individualKey.Status = newStatus;
                    hasChanges = true;
                    affectedParentKeyIds.Add(individualKey.ParentKeyId);
                }
            }

            if (becameOverdue)
            {
                overdueAssignmentsToNotify.Add(assignment);
            }

            if (assignment.DueDate.HasValue)
            {
                var daysUntilDue = (assignment.DueDate.Value.Date - today).TotalDays;
                if (daysUntilDue == 3 && !assignment.Reminder3Days)
                {
                    upcomingAssignmentsToNotify.Add(assignment);
                    assignment.Reminder3Days = true;
                    hasChanges = true;
                }
            }
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync();

            foreach (var parentKeyId in affectedParentKeyIds)
            {
                await RecalculateParentKeyAvailabilityAsync(parentKeyId);
            }
        }

        foreach (var assignment in overdueAssignmentsToNotify)
        {
            await SendOverdueNotificationAsync(assignment);
        }

        foreach (var assignment in upcomingAssignmentsToNotify)
        {
            await SendUpcomingDueNotificationAsync(assignment);
        }
    }

    private async Task SendOverdueNotificationAsync(AssignmentRecord assignment)
    {
        if (string.IsNullOrWhiteSpace(assignment.RequesterIGG))
            return;

        var requesterIgg = assignment.RequesterIGG.Trim().ToUpper();

        var staff = await _context.Set<VwStaff>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IGG.ToUpper() == requesterIgg);

        if (staff == null)
            return;

        var toEmail = staff.Email;

        if (string.IsNullOrWhiteSpace(toEmail))
            return;

        var recipientName = string.IsNullOrWhiteSpace(assignment.RequesterName)
            ? requesterIgg
            : assignment.RequesterName;

        var subject = $"Overdue Key Notice - {assignment.TagNumber}";

        var body =
$@"Hello {recipientName},

This is a reminder that the key assigned to you is now overdue and should be returned.

Key Number: {assignment.KeyId}
Tag Number: {assignment.TagNumber}
Floor: {assignment.FloorNumber}
Room: {assignment.RoomNumber}
Date Requested: {(assignment.DateRequested.HasValue ? assignment.DateRequested.Value.ToString("dd-MMM-yyyy") : "-")}
Due Date: {(assignment.DueDate.HasValue ? assignment.DueDate.Value.ToString("dd-MMM-yyyy") : "-")}
Assignment Type: {assignment.AssignmentType}

Please return the key as soon as possible.

Regards,
Facility Management & Administration";

        await _emailService.SendAsync(toEmail, subject, body);
    }

    private async Task SendUpcomingDueNotificationAsync(AssignmentRecord assignment)
    {
        if (string.IsNullOrWhiteSpace(assignment.RequesterIGG))
            return;

        var requesterIgg = assignment.RequesterIGG.Trim().ToUpper();

        var staff = await _context.Set<VwStaff>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IGG.ToUpper() == requesterIgg);

        if (staff == null)
            return;

        var toEmail = staff.Email;

        if (string.IsNullOrWhiteSpace(toEmail))
            return;

        var recipientName = string.IsNullOrWhiteSpace(assignment.RequesterName)
            ? requesterIgg
            : assignment.RequesterName;

        var subject = $"Upcoming Due Key - {assignment.TagNumber}";

        var body =
$@"Hello {recipientName},

This is a reminder that the key assigned to you will be overdue in 3 days.

Key Number: {assignment.KeyId}
Tag Number: {assignment.TagNumber}
Floor: {assignment.FloorNumber}
Room: {assignment.RoomNumber}
Date Requested: {(assignment.DateRequested.HasValue ? assignment.DateRequested.Value.ToString("dd-MMM-yyyy") : "-")}
Due Date: {(assignment.DueDate.HasValue ? assignment.DueDate.Value.ToString("dd-MMM-yyyy") : "-")}
Assignment Type: {assignment.AssignmentType}

Please ensure the key is returned on or before the due date.

Regards,
Facility Management & Administration";

        await _emailService.SendAsync(toEmail, subject, body);
    }

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var user = await GetCurrentUserAsync();

        if (user != null)
        {
            ViewBag.CurrentUserFullName = $"{user.Name}";
        }

        await next();
    }
}
