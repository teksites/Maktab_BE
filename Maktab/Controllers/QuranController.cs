using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Maktab.Attributes;
using MaktabDataContracts.Responses.Children;
using System.Collections.Generic;
using Users.Services;

namespace Maktab.Controllers
{
    [Route("api/quran")]
    [ApiController]
    [EnableCors("corspolicy")]
    public class QuranController : ControllerBase
    {
        private readonly IChildEducationalProfileService _childEducationalProfileService;

        public QuranController(IChildEducationalProfileService childEducationalProfileService)
        {
            _childEducationalProfileService = childEducationalProfileService;
        }

        [Authorize]
        [ApiAuthorize()]
        [HttpGet("surahs")]
        public ActionResult<IReadOnlyList<QuranSurahOptionResponse>> GetSurahs()
        {
            return Ok(_childEducationalProfileService.GetQuranSurahOptions());
        }
    }
}
