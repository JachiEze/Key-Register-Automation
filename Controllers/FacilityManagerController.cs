using KEYREGISTERAUTOMATION.Data;
using KEYREGISTERAUTOMATION.Models;
using KEYREGISTERAUTOMATION.Models.ViewModels;
using KEYREGISTERAUTOMATION.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KEYREGISTERAUTOMATION.Controllers
{
    [Authorize(Roles = "Facility Manager")]
    public class FacilityManagerController : BaseController
    {
        private const int MaxKeysPerBatch = 10;
        public FacilityManagerController(ApplicationDbContext context, IEmailService emailService) : base(context, emailService) { }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetBuildings()
        {
            var buildings = await _context.OfficeInfos
                .Select(o => o.Building)
                .Concat(_context.AllKeys.Select(k => k.Building))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return Json(buildings);
        }

        [HttpGet]
        public async Task<IActionResult> GetFloors(string building)
        {
            building = (building ?? "").Trim();

            if (string.IsNullOrWhiteSpace(building))
            {
                return Json(new List<string>());
            }

            var floorsList = await _context.OfficeInfos
                .Where(o => o.Building == building)
                .Select(o => o.FloorNumber)
                .Concat(
                    _context.AllKeys
                        .Where(k => k.Building == building)
                        .Select(k => k.FloorNumber)
                )
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToListAsync();

            var floors = floorsList
                .OrderBy(f =>
                {
                    if (int.TryParse(f, out var n)) return n;
                    return int.MaxValue;
                })
                .ThenBy(f => f)
                .ToList();

            return Json(floors);
        }

        [HttpGet]
        public async Task<IActionResult> GetRooms(string building, string floor)
        {
            building = (building ?? "").Trim();
            floor = (floor ?? "").Trim();

            if (string.IsNullOrWhiteSpace(building) || string.IsNullOrWhiteSpace(floor))
            {
                return Json(new List<string>());
            }

            var roomsList = await _context.OfficeInfos
                .Where(o => o.Building == building && o.FloorNumber == floor)
                .Select(o => o.RoomNumber)
                .Concat(
                    _context.AllKeys
                        .Where(k => k.Building == building && k.FloorNumber == floor)
                        .Select(k => k.RoomNumber)
                )
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToListAsync();

            var rooms = roomsList
                .OrderBy(r =>
                {
                    if (int.TryParse(r, out var n)) return n;
                    return int.MaxValue;
                })
                .ThenBy(r => r)
                .ToList();

            return Json(rooms);
        }

        [HttpGet]
        public async Task<IActionResult> GetKeyNumbers(string? building, string? floor, string? room)
        {
            IQueryable<Keys> query = _context.AllKeys.AsNoTracking();

            building = (building ?? "").Trim();
            floor = (floor ?? "").Trim();
            room = (room ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(building))
            {
                query = query.Where(k => k.Building == building);
            }

            if (!string.IsNullOrWhiteSpace(floor))
            {
                query = query.Where(k => k.FloorNumber == floor);
            }

            if (!string.IsNullOrWhiteSpace(room))
            {
                query = query.Where(k => k.RoomNumber == room);
            }

            var keyNumbers = await query
                .Where(k => !string.IsNullOrWhiteSpace(k.KeyId))
                .Select(k => k.KeyId.Trim())
                .Distinct()
                .OrderBy(k => k)
                .ToListAsync();

            return Json(keyNumbers);
        }

        [HttpGet]
        public async Task<IActionResult> AllKeysGrid(string? search)
        {
            IQueryable<Keys> query = _context.AllKeys.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(k =>
                    (k.KeyId != null && k.KeyId.Contains(search)) ||
                    (k.KeyCode != null && k.KeyCode.Contains(search)) ||
                    (k.Building != null && k.Building.Contains(search)) ||
                    (k.FloorNumber != null && k.FloorNumber.Contains(search)) ||
                    (k.RoomNumber != null && k.RoomNumber.Contains(search))
                );
            }

            var keys = await query.ToListAsync();

            keys = keys
                .OrderBy(k =>
                {
                    if (int.TryParse(k.FloorNumber, out var n)) return n;
                    return int.MaxValue;
                })
                .ThenBy(k => k.FloorNumber)
                .ToList();

            var vm = new AllKeysGrid
            {
                Search = search,
                Keys = keys
            };

            return PartialView("_KeyGrid", vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetKeyDetails(int id)
        {
            var key = await _context.AllKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Id == id);

            if (key == null)
            {
                return NotFound(new
                {
                    ok = false
                });
            }

            var rawIndividualKeys = await _context.IndividualKeys
                .AsNoTracking()
                .Where(x => x.ParentKeyId == key.Id)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.TagNumber,
                    x.Status
                })
                .ToListAsync();

            var individualKeys = rawIndividualKeys
                .Select((x, index) => new
                {
                    id = x.Id,
                    label = $"Key {index + 1}",
                    tagNumber = x.TagNumber,
                    status = string.IsNullOrWhiteSpace(x.Status) ? "Free" : x.Status
                })
                .ToList();

            return Json(new
            {
                ok = true,
                id = key.Id,
                keyId = key.KeyId,
                keyCode = key.KeyCode,
                building = key.Building,
                floor = key.FloorNumber,
                room = key.RoomNumber,
                individualKeys
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddKey([FromForm] AddKey model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter all the required information."
                });
            }

            model.KeyId = model.KeyId?.Trim() ?? "";
            model.Building = model.Building?.Trim() ?? "";
            model.FloorNumber = model.FloorNumber?.Trim() ?? "";
            model.RoomNumber = model.RoomNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(model.KeyId) ||
                string.IsNullOrWhiteSpace(model.Building) ||
                string.IsNullOrWhiteSpace(model.FloorNumber) ||
                string.IsNullOrWhiteSpace(model.RoomNumber))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter all the required information."
                });
            }

            if (model.NumberOfKeys < 1)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "You must add at least 1 key."
                });
            }

            if (model.NumberOfKeys > MaxKeysPerBatch)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = $"You can only add {MaxKeysPerBatch} keys at a time."
                });
            }

            var tagNumbers = (model.TagNumbers ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            if (tagNumbers.Count != model.NumberOfKeys)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = $"Please enter all the required information."
                });
            }

            if (tagNumbers.Count != tagNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Duplicate key tags are not allowed."
                });
            }

            if (tagNumbers.Any(t => string.Equals(t, model.KeyId, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "A key tag cannot be the same as the key number."
                });
            }

            var existingTagNumbers = await _context.IndividualKeys
                .Where(k => tagNumbers.Contains(k.TagNumber))
                .Select(k => k.TagNumber)
                .ToListAsync();


            if (existingTagNumbers.Any())
            {
                var messagePrefix = existingTagNumbers.Count == 1
                    ? "The following key tag already exists:"
                    : "The following key tags already exist:";

                return Conflict(new
                {
                    ok = false,
                    message = $"{messagePrefix} {string.Join(", ", existingTagNumbers)}"
                });
            }

            var existingExact = await _context.AllKeys.FirstOrDefaultAsync(k =>
                k.KeyId == model.KeyId &&
                k.Building == model.Building &&
                k.FloorNumber == model.FloorNumber &&
                k.RoomNumber == model.RoomNumber);
            if (existingExact != null)
            {
                return Conflict(new
                {
                    ok = false,
                    message = "A record already exists for that key. Use the update button to make changes to that key."
                });
            }

            var roomAlreadyAssigned = await _context.AllKeys.AnyAsync(k =>
                k.Building == model.Building &&
                k.FloorNumber == model.FloorNumber &&
                k.RoomNumber == model.RoomNumber);

            if (roomAlreadyAssigned)
            {
                return Conflict(new
                {
                    ok = false,
                    message = $"A key record is already registered for {model.Building}, floor {model.FloorNumber}, room {model.RoomNumber}."
                });
            }

            var keyRecord = new Keys
            {
                KeyId = model.KeyId,
                KeyCode = Keys.DefaultKeyCode,
                Building = model.Building,
                FloorNumber = model.FloorNumber,
                RoomNumber = model.RoomNumber,
                TotalNoofKeys = 0,
                NoofKeysAvaialble = 0
            };

            _context.AllKeys.Add(keyRecord);
            await _context.SaveChangesAsync();

            var newIndividualKeys = tagNumbers.Select(tag => new IndividualKey
            {
                ParentKeyId = keyRecord.Id,
                TagNumber = tag,
                Status = "Free"
            }).ToList();

            _context.IndividualKeys.AddRange(newIndividualKeys);

            keyRecord.TotalNoofKeys += newIndividualKeys.Count;
            keyRecord.NoofKeysAvaialble += newIndividualKeys.Count;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteKey([FromForm] int id, [FromForm] List<int>? individualKeyIds, [FromForm] bool deleteAll = false)
        {
            await SyncAssignmentStatusesAsync();

            var key = await _context.AllKeys.FirstOrDefaultAsync(k => k.Id == id);
            if (key == null)
            {
                return NotFound(new { ok = false });
            }

            individualKeyIds ??= new List<int>();
            individualKeyIds = individualKeyIds.Distinct().ToList();

            if (deleteAll)
            {
                individualKeyIds = await _context.IndividualKeys
                    .Where(k => k.ParentKeyId == key.Id)
                    .Select(k => k.Id)
                    .ToListAsync();
            }

            if (!individualKeyIds.Any())
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "You have not made any selection for deleting."
                });
            }

            var selectedKeys = await _context.IndividualKeys
                .Where(k => k.ParentKeyId == key.Id && individualKeyIds.Contains(k.Id))
                .ToListAsync();

            var nonFreeKeys = selectedKeys
                .Where(k => !string.Equals(
                    string.IsNullOrWhiteSpace(k.Status) ? "Free" : k.Status,
                    "Free",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nonFreeKeys.Any())
            {
                return Conflict(new
                {
                    ok = false,
                    message = "Keys that are currently assigned cannot be deleted."
                });
            }

            var selectedKeyIds = selectedKeys.Select(k => k.Id).ToList();

            var selectedTagNumbers = selectedKeys
                .Where(k => !string.IsNullOrWhiteSpace(k.TagNumber))
                .Select(k => k.TagNumber)
                .ToList();

            var hasActiveAssignmentsForSelectedKeys = await _context.AssignmentRecords.AnyAsync(a =>
                a.Status != "Returned" &&
                (
                    (a.IndividualKeyId.HasValue && selectedKeyIds.Contains(a.IndividualKeyId.Value)) ||
                    (!a.IndividualKeyId.HasValue && selectedTagNumbers.Contains(a.TagNumber))
                ));

            if (hasActiveAssignmentsForSelectedKeys)
            {
                return Conflict(new
                {
                    ok = false,
                    message = "One or more selected keys are currently assigned and cannot be deleted."
                });
            }

            _context.IndividualKeys.RemoveRange(selectedKeys);
            await _context.SaveChangesAsync();

            var hasRemainingChildren = await _context.IndividualKeys
                .AnyAsync(k => k.ParentKeyId == key.Id);

            if (hasRemainingChildren)
            {
                await RecalculateParentKeyAvailabilityAsync(key.Id);
            }
            else
            {
                _context.AllKeys.Remove(key);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                ok = true,
                message = deleteAll || !hasRemainingChildren
                    ? "All keys deleted successfully."
                    : selectedKeys.Count == 1
                        ? "Key deleted successfully."
                        : $"{selectedKeys.Count} keys deleted successfully."
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetKeyForUpdate(int id)
        {
            var key = await _context.AllKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Id == id);

            if (key == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Key record not found."
                });
            }

            var individualKeys = await _context.IndividualKeys
                .AsNoTracking()
                .Where(x => x.ParentKeyId == key.Id)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    tagNumber = x.TagNumber,
                    status = string.IsNullOrWhiteSpace(x.Status) ? "Free" : x.Status
                })
                .ToListAsync();

            return Json(new
            {
                ok = true,
                id = key.Id,
                keyId = key.KeyId,
                building = key.Building,
                floor = key.FloorNumber,
                room = key.RoomNumber,
                existingKeys = individualKeys
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateKey([FromForm] UpdateKey model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter all the required information."
                });
            }

            model.KeyId = model.KeyId?.Trim() ?? "";
            model.Building = model.Building?.Trim() ?? "";
            model.FloorNumber = model.FloorNumber?.Trim() ?? "";
            model.RoomNumber = model.RoomNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(model.KeyId) ||
                string.IsNullOrWhiteSpace(model.Building) ||
                string.IsNullOrWhiteSpace(model.FloorNumber) ||
                string.IsNullOrWhiteSpace(model.RoomNumber))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter all the required information."
                });
            }

            var keyRecord = await _context.AllKeys
                .Include(k => k.IndividualKeys)
                .FirstOrDefaultAsync(k => k.Id == model.Id);

            if (keyRecord == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Key record not found."
                });
            }

            var roomAlreadyExists = await _context.AllKeys.AnyAsync(k =>
                k.Id != model.Id &&
                k.RoomNumber == model.RoomNumber);

            if (roomAlreadyExists)
            {
                return Conflict(new
                {
                    ok = false,
                    message = $"A key record with room number {model.RoomNumber} already exists."
                });
            }

            model.ExistingKeys ??= new List<UpdateKeyExistingTag>();
            model.NewTagNumbers ??= new List<string>();

            foreach (var existing in model.ExistingKeys)
            {
                existing.TagNumber = existing.TagNumber?.Trim() ?? "";
                existing.Status = existing.Status?.Trim() ?? "Free";
            }

            var newTags = model.NewTagNumbers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            if (model.NumberOfNewKeys < 0)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Invalid number of new keys."
                });
            }

            if (model.NumberOfNewKeys > MaxKeysPerBatch)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = $"You can only add {MaxKeysPerBatch} keys at a time."
                });
            }

            if (newTags.Count != model.NumberOfNewKeys)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Please enter all the required information."
                });
            }

            var parentExistingIds = keyRecord.IndividualKeys.Select(x => x.Id).ToHashSet();
            if (model.ExistingKeys.Any(x => !parentExistingIds.Contains(x.Id)))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Invalid key tag selection."
                });
            }

            var allSubmittedTags = model.ExistingKeys
                .Select(x => x.TagNumber)
                .Concat(newTags)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (allSubmittedTags.Count != allSubmittedTags.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Duplicate key tags are not allowed."
                });
            }

            if (allSubmittedTags.Any(t => string.Equals(t, model.KeyId, StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "A key tag cannot be the same as the key number."
                });
            }

            var submittedExistingIds = model.ExistingKeys.Select(x => x.Id).ToList();

            var conflictingTags = await _context.IndividualKeys
                .Where(k =>
                    allSubmittedTags.Contains(k.TagNumber) &&
                    !submittedExistingIds.Contains(k.Id))
                .Select(k => k.TagNumber)
                .Distinct()
                .ToListAsync();

            if (conflictingTags.Any())
            {
                var messagePrefix = conflictingTags.Count == 1
                    ? "The following key tag already exists:"
                    : "The following key tags already exist:";

                return Conflict(new
                {
                    ok = false,
                    message = $"{messagePrefix} {string.Join(", ", conflictingTags)}"
                });
            }

            var oldKeyId = keyRecord.KeyId ?? "";
            var oldBuilding = keyRecord.Building ?? "";
            var oldFloor = keyRecord.FloorNumber ?? "";
            var oldRoom = keyRecord.RoomNumber ?? "";

            var oldTagById = keyRecord.IndividualKeys.ToDictionary(
                x => x.Id,
                x => x.TagNumber ?? ""
            );

            var oldStatusById = keyRecord.IndividualKeys.ToDictionary(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(x.Status) ? "Free" : x.Status
            );

            keyRecord.KeyId = model.KeyId;
            keyRecord.Building = model.Building;
            keyRecord.FloorNumber = model.FloorNumber;
            keyRecord.RoomNumber = model.RoomNumber;

            foreach (var submitted in model.ExistingKeys)
            {
                var dbChild = keyRecord.IndividualKeys.First(x => x.Id == submitted.Id);

                var originalTag = oldTagById.ContainsKey(submitted.Id) ? oldTagById[submitted.Id] : "";
                var originalStatus = oldStatusById.ContainsKey(submitted.Id) ? (oldStatusById[submitted.Id] ?? "Free").ToLower() : "free";

                if ((originalStatus == "issued" || originalStatus == "overdue") &&
                    !string.Equals(originalTag, submitted.TagNumber ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "Keys that are currently issued or overdue cannot be modified."
                    });
                }

                dbChild.TagNumber = submitted.TagNumber;
            }

            if (newTags.Any())
            {
                var newIndividualKeys = newTags.Select(tag => new IndividualKey
                {
                    ParentKeyId = keyRecord.Id,
                    TagNumber = tag,
                    Status = "Free"
                }).ToList();

                _context.IndividualKeys.AddRange(newIndividualKeys);
                keyRecord.TotalNoofKeys += newIndividualKeys.Count;
                keyRecord.NoofKeysAvaialble += newIndividualKeys.Count;
            }

            await _context.SaveChangesAsync();

            await RecalculateParentKeyAvailabilityAsync(keyRecord.Id);

            return Ok(new
            {
                ok = true
            });
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
            model.TagNumber = model.TagNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(model.RequesterIGG) ||
                string.IsNullOrWhiteSpace(model.RequesterName) ||
                string.IsNullOrWhiteSpace(model.Department) ||
                string.IsNullOrWhiteSpace(model.Division) ||
                string.IsNullOrWhiteSpace(model.CollectorType) ||
                string.IsNullOrWhiteSpace(model.AssignmentType) ||
                string.IsNullOrWhiteSpace(model.TagNumber))
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
                        ok = false
                    });
                }
            }
            else if (model.AssignmentType.Equals("Permanent", StringComparison.OrdinalIgnoreCase))
            {
                if (!model.DateRequested.HasValue)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "Please enter all the required information."
                    });
                }

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

            await SyncAssignmentStatusesAsync();

            var hasOverdueAssignment = await _context.AssignmentRecords.AnyAsync(a =>
                a.RequesterIGG == model.RequesterIGG &&
                a.Status == "Overdue");

            if (hasOverdueAssignment)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "This requester has an overdue key and cannot be assigned another key until it is returned."
                });
            }

            var individualKey = await _context.IndividualKeys
                .Include(k => k.ParentKey)
                .FirstOrDefaultAsync(k => k.TagNumber == model.TagNumber);

            if (individualKey == null || individualKey.ParentKey == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "That key tag does not exist."
                });
            }

            var individualKeyStatus = string.IsNullOrWhiteSpace(individualKey.Status) ? "Free" : individualKey.Status;

            if (individualKeyStatus.Equals("Issued", StringComparison.OrdinalIgnoreCase) ||
                individualKeyStatus.Equals("Overdue", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "That key is currently not available for assignments."
                });
            }

            if (model.AssignmentType != null && model.AssignmentType.Equals("Permanent", StringComparison.OrdinalIgnoreCase))
            {
                var hasPermanentForRequester = await _context.AssignmentRecords.AnyAsync(a =>
                    a.RequesterIGG == model.RequesterIGG &&
                    a.AssignmentType == "Permanent" &&
                    a.Status != "Returned" &&
                    a.KeyId == individualKey.ParentKey.KeyId &&
                    a.FloorNumber == individualKey.ParentKey.FloorNumber &&
                    a.RoomNumber == individualKey.ParentKey.RoomNumber);

                if (hasPermanentForRequester)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "This requester already has a permanent assignment for that key."
                    });
                }
            }

            if (model.AssignmentType != null && model.AssignmentType.Equals("Temporary", StringComparison.OrdinalIgnoreCase))
            {
                var hasTemporaryForRequester = await _context.AssignmentRecords.AnyAsync(a =>
                    a.RequesterIGG == model.RequesterIGG &&
                    a.AssignmentType == "Temporary" &&
                    a.Status != "Returned" &&
                    a.KeyId == individualKey.ParentKey.KeyId &&
                    a.FloorNumber == individualKey.ParentKey.FloorNumber &&
                    a.RoomNumber == individualKey.ParentKey.RoomNumber);

                if (hasTemporaryForRequester)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "This requester already has a temporary assignment for that key."
                    });
                }
            }

            model.KeyId = individualKey.ParentKey.KeyId;
            model.IndividualKeyId = individualKey.Id;
            model.TagNumber = individualKey.TagNumber;
            model.FloorNumber = individualKey.ParentKey.FloorNumber ?? "";
            model.RoomNumber = individualKey.ParentKey.RoomNumber ?? "";
            model.Status = "Issued";
            model.AssignedOn = DateTime.Now;
            model.ReturnedOn = null;

            individualKey.Status = "Issued";

            _context.AssignmentRecords.Add(model);
            await _context.SaveChangesAsync();

            await RecalculateParentKeyAvailabilityAsync(individualKey.ParentKeyId);

            return Ok(new
            {
                ok = true,
                message = "Assignment created successfully."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnAssignment([FromForm] ReturnAssignment model)
        {
            await SyncAssignmentStatusesAsync();

            var assignment = await _context.AssignmentRecords
                .FirstOrDefaultAsync(a => a.Id == model.Id);

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

            IndividualKey? individualKey = null;

            if (assignment.IndividualKeyId.HasValue)
            {
                individualKey = await _context.IndividualKeys
                    .FirstOrDefaultAsync(k => k.Id == assignment.IndividualKeyId.Value);
            }

            if (individualKey == null && !string.IsNullOrWhiteSpace(assignment.TagNumber))
            {
                individualKey = await _context.IndividualKeys
                    .FirstOrDefaultAsync(k => k.TagNumber == assignment.TagNumber);
            }

            if (individualKey == null)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "The key could not be found."
                });
            }

            assignment.Status = "Returned";
            assignment.ReturnedOn = DateTime.Now;
            assignment.Comment = model.Comment?.Trim();

            individualKey.Status = "Free";

            await _context.SaveChangesAsync();
            await RecalculateParentKeyAvailabilityAsync(individualKey.ParentKeyId);

            return Ok(new
            {
                ok = true,
                message = "Assignment marked as returned."
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchRequester(string igg)
        {
            igg = igg?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(igg))
            {
                return Json(new { ok = false });
            }

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
                return Json(new { ok = false });
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
        public async Task<IActionResult> GetRequesterActiveAssignments(string igg)
        {
            igg = igg?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(igg))
            {
                return Json(new
                {
                    ok = false,
                    assignments = new List<object>()
                });
            }

            await SyncAssignmentStatusesAsync();

            var assignments = await _context.AssignmentRecords
                .AsNoTracking()
                .Where(a =>
                    a.RequesterIGG == igg &&
                    (a.Status == "Issued" || a.Status == "Overdue"))
                .OrderByDescending(a => a.AssignedOn)
                .Select(a => new
                {
                    building = _context.IndividualKeys
                        .Where(k => k.Id == a.IndividualKeyId)
                        .Select(k => k.ParentKey != null ? k.ParentKey.Building : "")
                        .FirstOrDefault(),

                    floor = a.FloorNumber,
                    room = a.RoomNumber,
                    assignmentType = a.AssignmentType,
                    tagNumber = a.TagNumber,
                    status = a.Status
                })
                .ToListAsync();

            return Json(new
            {
                ok = true,
                assignments
            });
        }

        [HttpGet]
        public async Task<IActionResult> IssuedandOverdueGrid(string? search)
        {
            await SyncAssignmentStatusesAsync();

            IQueryable<AssignmentRecord> query = _context.AssignmentRecords.Where(a => a.Status == "Issued" || a.Status == "Overdue").AsNoTracking();

            var assignments = await query
                .OrderByDescending(a => a.AssignedOn)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            var vm = new AllAssignmentGrid
            {
                Search = search,
                Assignments = assignments
            };

            return PartialView("_IssuedandOverdueGrid", vm);
        }

        [HttpGet]
        public async Task<IActionResult> KeyManagerGrid(string? search)
        {
            await SyncAssignmentStatusesAsync();

            IQueryable<AssignmentRecord> query = _context.AssignmentRecords.AsNoTracking();

            var assignments = await query
                .OrderByDescending(a => a.AssignedOn)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            var vm = new AllAssignmentGrid
            {
                Search = search,
                Assignments = assignments
            };

            return PartialView("_KeyManager", vm);
        }

        [HttpGet]
        public async Task<IActionResult> KeyLookupGrid(string? search)
        {
            await SyncAssignmentStatusesAsync();

            var vm = new AllAssignmentGrid
            {
                Search = search,
                Assignments = new List<AssignmentRecord>()
            };

            if (string.IsNullOrWhiteSpace(search))
            {
                return PartialView("_KeyLookup", vm);
            }

            search = search.Trim();

            IQueryable<AssignmentRecord> query = _context.AssignmentRecords.AsNoTracking();

            query = query.Where(a =>
                (a.RequesterName != null && a.RequesterName.Contains(search)) ||
                (a.RequesterIGG != null && a.RequesterIGG.Contains(search)) ||
                (a.Department != null && a.Department.Contains(search)) ||
                (a.DelegateName != null && a.DelegateName.Contains(search)) ||
                (a.TagNumber != null && a.TagNumber.Contains(search)) ||
                (a.KeyId != null && a.KeyId.Contains(search)) ||
                (a.FloorNumber != null && a.FloorNumber.Contains(search)) ||
                (a.RoomNumber != null && a.RoomNumber.Contains(search))
            );

            vm.Assignments = await query
                .OrderByDescending(a => a.AssignedOn)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            return PartialView("_KeyLookup", vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetNextKeyNumber(string building, string floor, string room)
        {
            building = building?.Trim() ?? "";
            floor = floor?.Trim() ?? "";
            room = room?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(building) ||
                string.IsNullOrWhiteSpace(floor) ||
                string.IsNullOrWhiteSpace(room))
            {
                return Json(new
                {
                    ok = true,
                    existingCount = 0,
                    nextKeyNumber = 1
                });
            }

            var parentKey = await _context.AllKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k =>
                    k.Building == building &&
                    k.FloorNumber == floor &&
                    k.RoomNumber == room);

            if (parentKey == null)
            {
                return Json(new
                {
                    ok = true,
                    existingCount = 0,
                    nextKeyNumber = 1
                });
            }

            var existingCount = await _context.IndividualKeys
                .AsNoTracking()
                .CountAsync(k => k.ParentKeyId == parentKey.Id);

            return Json(new
            {
                ok = true,
                existingCount,
                nextKeyNumber = existingCount + 1
            });
        }

        [HttpGet]
        public async Task<IActionResult> DoesKeyRecordExist(string building, string floor, string room, string keyId)
        {
            building = building?.Trim() ?? "";
            floor = floor?.Trim() ?? "";
            room = room?.Trim() ?? "";
            keyId = keyId?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(building) || string.IsNullOrWhiteSpace(floor) || string.IsNullOrWhiteSpace(room) || string.IsNullOrWhiteSpace(keyId))
            {
                return Json(new { ok = true, exists = false });
            }

            var exists = await _context.AllKeys.AnyAsync(k =>
                k.KeyId == keyId &&
                k.Building == building &&
                k.FloorNumber == floor &&
                k.RoomNumber == room);

            return Json(new { ok = true, exists });
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableTagNumbers(string building, string floor, string room)
        {
            building = building?.Trim() ?? "";
            floor = floor?.Trim() ?? "";
            room = room?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(building) ||
                string.IsNullOrWhiteSpace(floor) ||
                string.IsNullOrWhiteSpace(room))
            {
                return Json(new List<string>());
            }

            await SyncAssignmentStatusesAsync();

            var tagNumbers = await _context.IndividualKeys
                .AsNoTracking()
                .Where(k =>
                    k.ParentKey != null &&
                    k.ParentKey.Building == building &&
                    k.ParentKey.FloorNumber == floor &&
                    k.ParentKey.RoomNumber == room &&
                    (k.Status == null || k.Status == "" || k.Status == "Free"))
                .OrderBy(k => k.TagNumber)
                .Select(k => k.TagNumber)
                .ToListAsync();

            return Json(tagNumbers);
        }

        [HttpGet]
        public async Task<IActionResult> GetAssignmentDetails(int id)
        {
            var assignment = await _context.AssignmentRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
            {
                return NotFound(new
                {
                    ok = false,
                    message = "Assignment not found."
                });
            }

            return Json(new
            {
                ok = true,
                id = assignment.Id,
                requesterIGG = assignment.RequesterIGG,
                requesterName = assignment.RequesterName,
                department = assignment.Department,
                division = assignment.Division,
                collectorType = assignment.CollectorType,
                delegateName = assignment.DelegateName,
                tagNumber = assignment.TagNumber,
                roomNumber = assignment.RoomNumber,
                floorNumber = assignment.FloorNumber,
                keyid = assignment.KeyId,
                assignmentType = assignment.AssignmentType,
                status = assignment.Status,
                dateRequested = assignment.DateRequested.HasValue
                    ? assignment.DateRequested.Value.ToString("dd-MMM-yyyy")
                    : "-",
                duration = assignment.Duration.HasValue
                    ? assignment.Duration.Value.ToString()
                    : "-",
                dueDate = assignment.DueDate.HasValue
                    ? assignment.DueDate.Value.ToString("dd-MMM-yyyy")
                    : "-",
                comment = assignment.Comment ?? ""
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchKeyTag(string tagNumber)
        {
            tagNumber = tagNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(tagNumber))
            {
                return Json(new { ok = false });
            }

            var individualKey = await _context.IndividualKeys
                .AsNoTracking()
                .Include(k => k.ParentKey)
                .FirstOrDefaultAsync(k => k.TagNumber == tagNumber);

            if (individualKey == null || individualKey.ParentKey == null)
            {
                return Json(new { ok = false });
            }

            return Json(new
            {
                ok = true,
                tagNumber = individualKey.TagNumber,
                keyId = individualKey.ParentKey.KeyId,
                building = individualKey.ParentKey.Building,
                floor = individualKey.ParentKey.FloorNumber,
                room = individualKey.ParentKey.RoomNumber
            });
        }
    }
}
