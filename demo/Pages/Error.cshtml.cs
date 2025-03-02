using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Blazorify.Flux.Demo.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel {
	public System.String? RequestId { get; set; }

	public System.Boolean ShowRequestId => !System.String.IsNullOrEmpty(this.RequestId);

	private readonly ILogger<ErrorModel> _logger;

	public ErrorModel(ILogger<ErrorModel> logger) {
		this._logger = logger;
	}

	public void OnGet() {
		this.RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier;
	}
}
