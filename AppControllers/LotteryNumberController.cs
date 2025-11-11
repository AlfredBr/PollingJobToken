using PollingJobToken.AppModels;
using PollingJobToken.Controllers;
using PollingJobToken.Services;

using Microsoft.AspNetCore.Mvc;

namespace PollingJobToken.AppControllers;

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
