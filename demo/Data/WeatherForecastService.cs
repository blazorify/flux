using System;
using System.Linq;
using System.Threading.Tasks;

namespace Blazorify.Flux.Demo.Data {
	public class WeatherForecastService {
		private static readonly String[] Summaries = new[]
		{
			"Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
		};

		public Task<WeatherForecast[]> GetForecastAsync(DateTime startDate) {
			return Task.FromResult(Enumerable.Range(1, 5).Select(index => new WeatherForecast {
				Date = startDate.AddDays(index),
				TemperatureC = Random.Shared.Next(-20, 55),
				Summary = Summaries[Random.Shared.Next(Summaries.Length)]
			}).ToArray());
		}
	}
}
