using KEYREGISTERAUTOMATION.Data;
using KEYREGISTERAUTOMATION.Models;
using KEYREGISTERAUTOMATION.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KEYREGISTERAUTOMATION.Controllers
{
    [Authorize(Roles = "Facility Manager")]
    public class FacilityManagerController : BaseController
    {
        public FacilityManagerController(ApplicationDbContext context) : base(context) { }

        public IActionResult Index() => View();


        [HttpGet]
        public async Task<IActionResult> GetBuildings()
        {
            var buildings = await _context.OfficeInfos
                .Select(o => o.Building)
                .Distinct()
                .OrderBy(b => b)
                .ToListAsync();

            return Json(buildings);
        }

        [HttpGet]
        public async Task<IActionResult> GetFloors(string building)
        {
            var floors = await _context.OfficeInfos
                .Where(o => o.Building == building)
                .Select(o => o.FloorNumber)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();

            return Json(floors);
        }

        [HttpGet]
        public async Task<IActionResult> GetRooms(string building, string floor)
        {
            var rooms = await _context.OfficeInfos
                .Where(o => o.Building == building && o.FloorNumber == floor)
                .Select(o => o.RoomNumber)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            return Json(rooms);
        }

        [HttpGet]
        public async Task<IActionResult> AllKeysGrid(string? search)
        {
            var query = _context.KeyTags.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(k =>
                    (k.KeyId != null && k.KeyId.Contains(search)) ||
                    (k.Building != null && k.Building.Contains(search)) ||
                    (k.FloorNumber != null && k.FloorNumber.Contains(search)) ||
                    (k.RoomNumber != null && k.RoomNumber.Contains(search))
                );
            }

            var keys = await query
                .OrderByDescending(k => k.Id)
                .ToListAsync();

            var vm = new AllKeysGrid
            {
                Search = search,
                Keys = keys
            };

            return PartialView("_KeyGrid", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddKey([FromForm] AddKeyVm model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { ok = false, message = "Please enter all the required information." });

            if (model.KeyId != model.KeyCode)
                return Conflict(new { ok = false, message = "Key Id and Key Code must be the same." });

            string keyId = model.KeyId.Trim();
            string keyCode = model.KeyCode.Trim();
            string building = model.Building.Trim();
            string floor = model.FloorNumber.Trim();
            string room = model.RoomNumber.Trim();

            var officeExists = await _context.OfficeInfos.AnyAsync(o =>
                o.Building == building &&
                o.FloorNumber == floor &&
                o.RoomNumber == room);

            var exactMatch = await _context.KeyTags.FirstOrDefaultAsync(k =>
                k.KeyId == keyId &&
                k.KeyCode == keyCode &&
                k.Building == building &&
                k.FloorNumber == floor &&
                k.RoomNumber == room
            );

            if (exactMatch != null)
            {
                exactMatch.TotalNoofKeys += 1;
                exactMatch.NoofKeysAvaialble += 1;

                await _context.SaveChangesAsync();
                return Ok(new { ok = true, updated = true });
            }

            var roomAlreadyAssigned = await _context.KeyTags.AnyAsync(k =>
                k.Building == building &&
                k.FloorNumber == floor &&
                k.RoomNumber == room
            );

            if (roomAlreadyAssigned)
            {
                return Conflict(new
                {
                    ok = false,
                    message = $"A key record already exists for Building {building}, Floor {floor}, Room {room}."
                });
            }

            var keyAlreadyExists = await _context.KeyTags.AnyAsync(k =>
                k.KeyId == keyId &&
                k.KeyCode == keyCode
            );

            if (keyAlreadyExists)
            {
                return Conflict(new
                {
                    ok = false,
                    message = $"A record with Key ID '{keyId}' and Key Code '{keyCode}' already exists."
                });
            }

            var entity = new AllKeyTags
            {
                KeyId = keyId,
                KeyCode = keyCode,
                Building = building,
                FloorNumber = floor,
                RoomNumber = room,
                TotalNoofKeys = 1,
                NoofKeysAvaialble = 1
            };

            _context.KeyTags.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new { ok = true, created = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnAssignment(int id)
        {
            var assignment = await _context.AssignmentRecords.FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Assignment not found."
                });
            }

            if (string.Equals(assignment.Status, "Returned", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "This assignment has already been returned."
                });
            }

            var key = await _context.KeyTags.FirstOrDefaultAsync(k => k.KeyId == assignment.KeyId);

            if (key == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "The related key record could not be found."
                });
            }

            assignment.Status = "Returned";

            if (key.NoofKeysAvaialble < key.TotalNoofKeys)
            {
                key.NoofKeysAvaialble += 1;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Assignment marked as returned."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteKey(int id)
        {
            var key = await _context.KeyTags.FindAsync(id);
            if (key == null)
                return NotFound();

            var hasIssuedAssignments = await _context.AssignmentRecords
                .AnyAsync(a => a.KeyId == key.KeyId && a.Status == "Issued");

            if (hasIssuedAssignments)
            {
                return Conflict(new
                {
                    ok = false,
                    message = "This key cannot be deleted because it has active issued assignments."
                });
            }

            _context.KeyTags.Remove(key);
            await _context.SaveChangesAsync();

            return Ok(new { ok = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateKeyRoom([FromForm] UpdateKeyRoomVm model)
        {

            var key = await _context.KeyTags.FindAsync(model.Id);

            string newRoom = model.RoomNumber.Trim();

            var roomExists = await _context.OfficeInfos.AnyAsync(o =>
                o.Building == key.Building &&
                o.FloorNumber == key.FloorNumber &&
                o.RoomNumber == newRoom);

            var roomAlreadyAssigned = await _context.KeyTags.AnyAsync(k =>
                k.Id != key.Id &&
                k.Building == key.Building &&
                k.FloorNumber == key.FloorNumber &&
                k.RoomNumber == newRoom);

            if (roomAlreadyAssigned)
                return Conflict(new { ok = false, message = "Another key already exists for that room." });

            key.RoomNumber = newRoom;
            await _context.SaveChangesAsync();

            return Ok(new { ok = true });
        }

        public IActionResult CreateAssignmentForm()
        {
            return PartialView("_AssignmentForm", new AssignmentRecord());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAssignment(AssignmentRecord model)
        {
            model.RequesterIGG = model.RequesterIGG?.Trim() ?? "";
            model.RequesterName = model.RequesterName?.Trim() ?? "";
            model.Department = model.Department?.Trim() ?? "";
            model.Division = model.Division?.Trim() ?? "";
            model.CollectorType = model.CollectorType?.Trim() ?? "";
            model.DelegateName = model.DelegateName?.Trim();
            model.AssignmentType = model.AssignmentType?.Trim() ?? "";
            model.KeyId = model.KeyId?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(model.RequesterIGG) ||
                string.IsNullOrWhiteSpace(model.RequesterName) ||
                string.IsNullOrWhiteSpace(model.Department) ||
                string.IsNullOrWhiteSpace(model.Division) ||
                string.IsNullOrWhiteSpace(model.CollectorType) ||
                string.IsNullOrWhiteSpace(model.AssignmentType) ||
                string.IsNullOrWhiteSpace(model.KeyId))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter all the required information."
                });
            }

            if (model.CollectorType.Equals("Delegate", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(model.DelegateName))
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "Please enter all the required information."
                    });
                }
            }
            else if (model.CollectorType.Equals("Requester", StringComparison.OrdinalIgnoreCase))
            {
                model.DelegateName = null;
            }
            else
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter all the required information."
                });
            }

            if (model.AssignmentType.Equals("Temporary", StringComparison.OrdinalIgnoreCase))
            {
                if (!model.DateRequested.HasValue ||
                    !model.Duration.HasValue ||
                    model.Duration.Value <= 0 ||
                    !model.DueDate.HasValue)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "Please enter all the required information."
                    });
                }

                if (model.DueDate.Value.Date < model.DateRequested.Value.Date)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "Due date cannot be earlier than date requested."
                    });
                }
            }
            else if (model.AssignmentType.Equals("Permanent", StringComparison.OrdinalIgnoreCase))
            {
                model.DateRequested = null;
                model.Duration = null;
                model.DueDate = null;
            }
            else
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter all the required information."
                });
            }

            var key = await _context.KeyTags.FirstOrDefaultAsync(k => k.KeyId == model.KeyId);

            if (key == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter a correct Key ID"
                });
            }

            if (key.NoofKeysAvaialble <= 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = $"Key '{model.KeyId}' is currently unavailable."
                });
            }

            model.Status = "Issued";

            key.NoofKeysAvaialble -= 1;

            _context.AssignmentRecords.Add(model);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                message = "Assignment created successfully."
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchRequester(string igg)
        {
            if (string.IsNullOrWhiteSpace(igg))
            {
                return BadRequest(new { ok = false, message = "Please enter an IGG." });
            }

            igg = igg.Trim();

            var staff = await _context.Set<VwStaff>()
                .AsNoTracking()
                .Where(s => s.IGG == igg)
                .Select(s => new
                {
                    igg = s.IGG,
                    name = s.Name,
                    department = s.Department,
                    division = s.Division
                })
                .FirstOrDefaultAsync();

            if (staff == null)
            {
                return NotFound(new { ok = false, message = $"No staff record found for IGG '{igg}'." });
            }

            return Json(new
            {
                ok = true,
                igg = staff.igg,
                name = staff.name,
                department = staff.department,
                division = staff.division
            });
        }

        [HttpGet]
        public async Task<IActionResult> AllAssignmentsGrid(string? search)
        {
            var query = _context.AssignmentRecords.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                DateTime parsedDate;
                bool isDateSearch = DateTime.TryParse(search, out parsedDate);

                query = query.Where(a =>
                    (a.RequesterIGG != null && a.RequesterIGG.Contains(search)) ||
                    (a.RequesterName != null && a.RequesterName.Contains(search)) ||
                    (a.DelegateName != null && a.DelegateName.Contains(search)) ||
                    (a.KeyId != null && a.KeyId.Contains(search)) ||
                    (a.Status != null && a.Status.Contains(search))
                );
            }

            var assignments = await query
                .OrderBy(a => a.Status)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            var vm = new AllAssignmentGrid
            {
                Search = search,
                Assignments = assignments
            };

            return PartialView("_AssignmentGrid", vm);
        }
    }

    public class AddKeyVm
    {
        public string KeyId { get; set; } = "";
        public string KeyCode { get; set; } = "";
        public string Building { get; set; } = "";
        public string RoomNumber { get; set; } = "";
        public string FloorNumber { get; set; } = "";
    }

    public class UpdateKeyRoomVm
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; } = "";
    }
}