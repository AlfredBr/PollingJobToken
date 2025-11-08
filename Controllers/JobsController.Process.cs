using api.Models;
using api.Services;

namespace api.Controllers;

public partial class JobsController
{
 private partial async Task ProcessJobAsync(string jobId)
 {
 try
 {
 _jobstore.SetProcessing(jobId);
 // Simulate some work
 await Task.Delay(TimeSpan.FromSeconds(3));
 // Put any default data here. Replace later.
 _jobstore.SetCompleted(jobId, new { echo = jobId }, message: "Done");
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error processing job {JobId}", jobId);
 _jobstore.SetFailed(jobId, ex.Message);
 }
 }
}
