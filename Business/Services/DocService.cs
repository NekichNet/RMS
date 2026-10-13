using Business.Database;
using Business.Logging;
using Business.Models;
using Business.Models.Enums;
using Business.Models.Util;
using Business.Services.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace Business.Services
{
    public class DocService : BusinessService
    {
        private readonly ILogger<DocService> _logger;
        private readonly AppDbContext _db;
        private readonly UnitPermissionService _unitPermissionService;

        public DocService(
            UnitPermissionService unitPermissionService,
            AppDbContext db,
            ILogger<DocService> logger)
        {
            _unitPermissionService = unitPermissionService;
            _db = db;
            _logger = logger;
        }

		public async Task<ActionResult<Doc>> CreateAsync(string title)
        {
            ActionResult<Doc> action = new ActionResult<Doc>(_logger);

            try
            {
                if (Actor == null)
                    return action.FormFailure("Doc creation restricted. Unauthorized", eventId: EventIds.Unauthorized);
                if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.UploadDocs)).IsSuccess)
                    return action.FormFailure("Doc creation restricted", eventId: EventIds.Forbidden);

                action.Value = new Doc()
                {
                    Title = title,
                    UploadedTime = DateTime.UtcNow,
                    Author = Actor
                };
                await _db.Docs.AddAsync(action.Value);
                await _db.SaveChangesAsync();

                action.FormSuccess($"Doc {title} with ID {action.Value.Id} created", eventId: EventIds.Created);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		public async Task<ActionResult<Doc>> GetAsync(int docId)
        {
            ActionResult<Doc> action = new ActionResult<Doc>(_logger);

            try
            {
                Doc? doc = await _db.Docs.FindAsync(docId);
                if (doc == null)
					return action.FormFailure($"Doc with ID {docId} not found", eventId: EventIds.NotFound);
				if (doc.IsHidden)
                {
                    if (Actor == null)
						return action.FormFailure($"Doc with ID {docId} not found", eventId: EventIds.NotFound);
					if (!(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.SeeHiddenDocs)).IsSuccess)
						return action.FormFailure($"Doc with ID {docId} not found", eventId: EventIds.NotFound);
				}

                action.Value = doc;

                action.FormSuccess($"Doc {doc.Title} with ID {docId} found", eventId: EventIds.Read);
            }
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }

		public async Task<ActionResult<List<Doc>>> GetAllAsync()
		{
			ActionResult<List<Doc>> action = new ActionResult<List<Doc>>(_logger);

			try
			{
                if (Actor != null)
                {
					if ((await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.SeeHiddenDocs)).IsSuccess)
                    {
						action.Value = await _db.Docs.ToListAsync();
					}
				}
                else
                {
					action.Value = await _db.Docs.Where(d => !d.IsHidden).ToListAsync();
				}

                action.FormSuccess("All docs list formed. Length: " + action.Value.Count(),
                    eventId: action.Value.Count() > 0 ? EventIds.Read : EventIds.NoData);
			}
			catch (Exception ex)
			{
				action.FormException(ex);
			}

			return action;
		}

        public async Task<EmptyAction> DeleteAsync(int docId)
        {
            EmptyAction action = new EmptyAction(_logger);
            
            try
            {
                if (Actor == null)
                    return action.FormFailure("Deleting doc restricted. Unauthorized", eventId: EventIds.Unauthorized);
                
                Doc? doc = await _db.Docs.FindAsync(docId);
                if (doc == null)
                    return action.FormFailure($"Deleting doc failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
                if (doc.IsHidden && !(await _unitPermissionService.CheckUnitHasPermissionAsync(Actor.DiscordId, PermissionType.SeeHiddenDocs)).IsSuccess)
                    return action.FormFailure($"Deleting doc failed. Doc with ID {docId} not found", eventId: EventIds.NotFound);
                if (doc.AuthorId != Actor.DiscordId && !(await _unitPermissionService.CheckUnitIsAdminAsync(Actor.DiscordId)).IsSuccess)
                    return action.FormFailure($"Deleting doc {doc.Title} with ID {docId} restricted", eventId: EventIds.Forbidden);

                _db.Docs.Remove(doc);
                await _db.SaveChangesAsync();

                action.FormSuccess($"Doc {doc.Title} with ID {docId} deleted", eventId: EventIds.Deleted);
			}
            catch (Exception ex)
            {
                action.FormException(ex);
            }

            return action;
        }
	}
}