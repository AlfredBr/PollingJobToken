using api.AppModels;
using api.Controllers;
using api.Services;

using Microsoft.AspNetCore.Mvc;

namespace api.AppControllers;

[ApiController]
[Route("jobs/lottery")]
public class LotteryNumberController
    : JobSubmissionControllerBase<LotteryNumberRequest, LotteryNumberResponse>
{
    public LotteryNumberController(
        IJobStore store,
        IJobProcessor<LotteryNumberRequest, LotteryNumberResponse> processor,
        ILogger<LotteryNumberController> logger
    )
        : base(store, processor, logger) { }

    [HttpPost]
    public ActionResult Submit([FromBody] LotteryNumberRequest request)
    {        
        return SubmitJobInternal(request);
    }
}
