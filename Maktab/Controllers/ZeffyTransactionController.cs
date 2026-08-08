using Microsoft.AspNetCore.Mvc;
using MaktabDataContracts.Requests.Zeffy;
using MaktabDataContracts.Responses.Zeffy;
using Zeffy.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using Maktab.Attributes;
using Microsoft.AspNetCore.Cors;
using Newtonsoft.Json;
using System.Text.Json;
using Users.Services;

namespace Maktab.Api.Controllers
{
    [EnableCors("corspolicy")]
    [ApiController]
    [Route("api/[controller]")]
    public class ZeffyTransactionController : ControllerBase
    {
        private readonly IZeffyTransactionService _zeffyService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;

        public ZeffyTransactionController(
            IZeffyTransactionService zeffyService,
            IDataAccessVerificationService dataAccessVerificationService)
        {
            _zeffyService = zeffyService;
            _dataAccessVerificationService = dataAccessVerificationService;
        }

        /// <summary>
        /// Save Zeffy transaction (e.g. webhook from Zeffy).
        /// </summary>
        //[HttpPost]
        //public async Task<IActionResult> SaveZeffyTransaction([FromBody] ZeffyRequest request)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    await _zeffyService.SaveZeffyTransaction(request);
        //    return Ok();
        //}
        //[ApiKeyAuthorize]
        //[HttpPost]
        //public async Task<IActionResult> ReceiveZeffyWebhookAsync([FromBody] string rawBody)
        //{

        //    if (string.IsNullOrWhiteSpace(rawBody))
        //        return BadRequest("Invalid payload");

        //    List<ZeffyRequest>? zeffyList;
        //    try
        //    {
        //        zeffyList = JsonConvert.DeserializeObject<List<ZeffyRequest>>(rawBody);
        //    }
        //    catch (JsonException ex)
        //    {
        //        // log ex if needed
        //        return BadRequest("Invalid Zeffy JSON format");
        //    }

        //    if (zeffyList == null || !zeffyList.Any())
        //        return BadRequest("Invalid payload");

        //    foreach (var zeffy in zeffyList)
        //    {
        //        await _zeffyService.SaveZeffyTransaction(zeffy);
        //    }

        //    return Ok(new { message = "Zeffy donations received successfully" });
        //    //if (zeffyList == null || !zeffyList.Any())
        //    //    return BadRequest("Invalid payload");

        //    //foreach (var zeffy in zeffyList)
        //    //{
        //    //    await _zeffyService.SaveZeffyTransaction(zeffy);
        //    //}

        //    //return Ok(new { message = "Zeffy donations received successfully" });
        //}

        [ApiKeyAuthorize]
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveZeffyWebhookAsync([FromBody] JsonElement body)
        {
            // Get the raw JSON text (this will be the full array as a string)
            var rawBody = body.GetRawText();

            if (string.IsNullOrWhiteSpace(rawBody))
                return BadRequest("Invalid payload");

            List<ZeffyRequest>? zeffyList;
            try
            {
                // Uses Newtonsoft.Json and your CustomFieldListConverter
                zeffyList = JsonConvert.DeserializeObject<List<ZeffyRequest>>(rawBody);
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest("Invalid Zeffy JSON format");
            }

            if (zeffyList == null || !zeffyList.Any())
                return BadRequest("Invalid payload");

            foreach (var zeffy in zeffyList)
            {
                await _zeffyService.SaveZeffyTransaction(zeffy);
            }

            return Ok(new { message = "Zeffy donations received successfully" });
        }

        /// <summary>
        /// Returns all Zeffy donations.
        /// </summary>
        [ApiAuthorize(false, false, MaktabDataContracts.Enums.UserRoleType.Admin)]
        [HttpGet]
        public async Task<ActionResult<List<ZeffyResponse>>> GetAllZeffyDonations()
        {
            var result = await _zeffyService.GetAllZeffyDonations();
            return Ok(result);
        }

        // -------- extra endpoints if you expose more service methods --------

        [ApiAuthorize]
        [HttpGet("zeffy/by-zeffy/{zeffyId:guid}")]
        public async Task<ActionResult<ZeffyResponse>> GetByZeffyId(Guid zeffyId)
        {
            var result = await _zeffyService.GetByZeffyId(zeffyId);
            if (result == null) return NotFound();
            if (!await HasFamilyAccessAsync(result.FamilyId).ConfigureAwait(false))
            {
                return Forbid();
            }
            return Ok(result);
        }

        [ApiAuthorize]
        [HttpGet("zeffy/by-family/{familyId:guid}")]
        public async Task<ActionResult<IEnumerable<ZeffyResponse>>> GetByFamilyId(Guid familyId)
        {
            var result = await _zeffyService.GetByFamilyId(familyId);
            return Ok(result);
        }

        [ApiAuthorize]
        [HttpGet("zeffy/by-transaction/{studentCourseTransactionId:guid}")]
        public async Task<ActionResult<IEnumerable<ZeffyResponse>>> GetByStudentCourseTransactionId(Guid studentCourseTransactionId)
        {
            var result = (await _zeffyService.GetByStudentCourseTransactionId(studentCourseTransactionId).ConfigureAwait(false)).ToList();
            if (!await HasFamilyAccessAsync(result.Select(item => item.FamilyId)).ConfigureAwait(false))
            {
                return Forbid();
            }
            return Ok(result);
        }

        [ApiAuthorize]
        [HttpGet("zeffy/by-paymentcode/{paymentCode}")]
        public async Task<ActionResult<IEnumerable<ZeffyResponse>>> GetByPaymentCode(string paymentCode)
        {
            var result = (await _zeffyService.GetByPaymentCode(paymentCode).ConfigureAwait(false)).ToList();
            if (!await HasFamilyAccessAsync(result.Select(item => item.FamilyId)).ConfigureAwait(false))
            {
                return Forbid();
            }
            return Ok(result);
        }

        [ApiAuthorize]
        [HttpGet("zeffy/families/{familyId:guid}/{paymentCode}")]
        public async Task<ActionResult<IEnumerable<ZeffyResponse>>> GetByFamilyAndPaymentCode(Guid familyId, string paymentCode)
        {
            var result = await _zeffyService.GetByFamilyAndPaymentCode(familyId, paymentCode);
            return Ok(result);
        }

        [ApiAuthorize(false, false, MaktabDataContracts.Enums.UserRoleType.Admin)]
        [HttpDelete("{zeffyId:guid}")]
        public async Task<IActionResult> Delete(Guid zeffyId, [FromQuery] bool hardDelete = false)
        {
            var success = await _zeffyService.Delete(zeffyId, hardDelete);
            if (!success) return NotFound();
            return NoContent();
        }

        [ApiAuthorize(false, false, MaktabDataContracts.Enums.UserRoleType.Admin)]
        [HttpPut("{zeffyId:guid}")]
        public async Task<IActionResult> Update(Guid zeffyId, [FromBody] ZeffyResponse zeffy)
        {
            if (zeffy == null || zeffy.ZeffyId != zeffyId)
                return BadRequest("ZeffyId mismatch.");

            var success = await _zeffyService.Update(zeffy);
            if (!success) return NotFound();

            return NoContent();
        }

        private async Task<bool> HasFamilyAccessAsync(Guid familyId)
        {
            var sessionContext = await GetRequiredSessionContext().ConfigureAwait(false);
            if (_dataAccessVerificationService.HasElevatedAccess(sessionContext.UserRoles))
            {
                return true;
            }

            return familyId == sessionContext.FamilyId;
        }

        private async Task<bool> HasFamilyAccessAsync(IEnumerable<Guid> familyIds)
        {
            var familyList = familyIds
                .Where(familyId => familyId != Guid.Empty)
                .Distinct()
                .ToList();

            if (familyList.Count == 0)
            {
                return true;
            }

            var sessionContext = await GetRequiredSessionContext().ConfigureAwait(false);
            if (_dataAccessVerificationService.HasElevatedAccess(sessionContext.UserRoles))
            {
                return true;
            }

            return familyList.All(familyId => familyId == sessionContext.FamilyId);
        }

        private async Task<SessionAccessContext> GetRequiredSessionContext()
        {
            if (!Request.Headers.TryGetValue("Session_Info", out var sessionHeader)
                || !Guid.TryParse(sessionHeader, out var sessionId)
                || sessionId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Session header not found or invalid.");
            }

            var sessionContext = await _dataAccessVerificationService.GetSessionAccessContext(sessionId).ConfigureAwait(false);
            if (sessionContext == null || sessionContext.UserId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("No active session found.");
            }

            return sessionContext;
        }
    }
}
