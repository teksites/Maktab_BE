using Application.Users.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Maktab.Attributes;
using MaktabDataContracts.Models;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Responses.Children;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Users.Services;

namespace Maktab.Controllers
{

    [Route("api")]
    [ApiController]
    [ApiAuthorize()]
    public class ChildrenController : ControllerBase
    {
        private readonly IUserChildrenService _childrenService;

        private readonly ILogger<ChildrenController> _logger;
        private readonly IUserService userService;

        public ChildrenController(IUserChildrenService childrenService, IUserService service, ILogger<ChildrenController> logger)
        {
            _childrenService = childrenService;
            _logger = logger;
            userService = service;
        }

        [Authorize]
        [HttpGet("children/{childId:guid}")]
        [EnableCors("corspolicy")]
        public async Task<MaktabApiResult<ChildResponse>> GetChild(Guid childId)
        {
            return await _childrenService.GetChild(childId).ConfigureAwait(false);
        }
        
        [Authorize]
        [HttpGet("families/{familyId:guid}/children")]
        [EnableCors("corspolicy")]
        public async Task<IEnumerable<MaktabApiResult<ChildResponse>>> GetUserChilds(Guid familyId)
        {
            return await _childrenService.GetUserChilds(familyId).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("families/{familyId:guid}/children/add")]
        [EnableCors("corspolicy")]
        public async Task<MaktabApiResult<ChildResponse>> AddUserChild(Guid familyId,AddChildRequest child)
        {
            return await _childrenService.AddChild(child).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("children/{childId:guid}/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteChild(Guid childId, bool ifHardDelete = false)
        {
            return await _childrenService.DeleteChild(childId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("families/{familyId:guid}/children/delete")]
        [EnableCors("corspolicy")]
        public async Task<bool> DeleteChildByUserId(Guid familyId, bool ifHardDelete = false)
        {
            return await _childrenService.DeleteUserChilds(familyId, ifHardDelete).ConfigureAwait(false);
        }

        [Authorize]
        [HttpPost("children/check")]
        [EnableCors("corspolicy")]
        public async Task<bool> CheckIfUserChildExisit(UserChildToVerify clientchild)
        {

            return await _childrenService.CheckIfChildExisit(clientchild).ConfigureAwait(false);
        }
    }
}
