using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using essentialMix.Web;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NWebsec.Core.Common.Middleware.Options;

// ReSharper disable once CheckNamespace
namespace essentialMix.Extensions;

public static class IApplicationBuilderExtension
{
	private const string CSP_TRUSTED_DOMAINS_KEY = "CspTrustedDomains";
	private const string CSP_TRUSTED_CONNECT_DOMAINS_KEY = "CspTrustedConnectDomains";

	[NotNull]
	public static IApplicationBuilder UseRedirectWithStatusCode([NotNull] this IApplicationBuilder thisValue, string errorHandlerPathTemplate = null)
	{
		errorHandlerPathTemplate = errorHandlerPathTemplate?.Trim(Path.AltDirectorySeparatorChar, ' ').ToNullIfEmpty() ?? "/error/{0}";
		return thisValue.UseStatusCodePagesWithReExecute(errorHandlerPathTemplate);
	}

	[NotNull]
	public static IApplicationBuilder UseDefaultExceptionDelegate([NotNull] this IApplicationBuilder thisValue, ILogger logger = null)
	{
		return UseDefaultExceptionDelegate(thisValue, null, logger);
	}

	[NotNull]
	public static IApplicationBuilder UseDefaultExceptionDelegate([NotNull] this IApplicationBuilder thisValue, Func<HttpContext, Exception, ILogger, Task> onError, ILogger logger = null)
	{
		onError ??= async (context, exception, log) =>
		{
			log?.LogError(exception, exception.Message);
			context.Response.ContentType = "text/html";
			await context.Response.WriteAsync(new ResponseStatus
			{
				StatusCode = (HttpStatusCode)context.Response.StatusCode,
				Exception = exception
			}.ToString()
												.Replace(Environment.NewLine, $"{Environment.NewLine}<br />"));
		};

		thisValue.UseExceptionHandler(app =>
		{
			app.Run(async context =>
			{
				IExceptionHandlerFeature contextFeature = context.Features.Get<IExceptionHandlerFeature>();
				if (contextFeature == null) return;
				await onError(context, contextFeature.Error, logger);
			});
		});

		return thisValue;
	}

	[NotNull]
	public static IApplicationBuilder UseSecurityHeaders([NotNull] this IApplicationBuilder thisValue, [NotNull] IConfiguration configuration)
	{
		ForwardedHeadersOptions forwardingOptions = new ForwardedHeadersOptions
		{
			ForwardedHeaders = ForwardedHeaders.All
		};

		forwardingOptions.KnownNetworks.Clear();
		forwardingOptions.KnownProxies.Clear();

		thisValue.UseForwardedHeaders(forwardingOptions);

		thisValue.UseReferrerPolicy(options => options.NoReferrer());

		// CSP Configuration to be able to use external resources
		string[] cspTrustedDomains = configuration.GetSection(CSP_TRUSTED_DOMAINS_KEY).Get<string[]>();
		thisValue.UseCsp(csp =>
		{
			csp.Sandbox(options =>
				{
					options.AllowSameOrigin()
							.AllowScripts()
							.AllowForms()
							.AllowModals()
							.AllowPopups()
							.AllowPopupsToEscapeSandbox();
				})
				.FrameAncestors(options =>
				{
					options.None();
				})
				.BaseUris(options =>
				{
					options.Self();
				})
				.ObjectSources(options =>
				{
					options.None();
				});

			if (cspTrustedDomains is { Length: > 0 })
			{
				csp.ImageSources(options =>
					{
						options.CustomSources(cspTrustedDomains.Prepend("data:").ToArray())
								.Self();
					})
					.FontSources(options =>
					{
						options.CustomSources(cspTrustedDomains)
								.Self();
					})
					.ScriptSources(options =>
					{
						options.CustomSources(cspTrustedDomains)
								.Self()
								.UnsafeInline()
								.UnsafeEval();
					})
					.StyleSources(options =>
					{
						options.CustomSources(cspTrustedDomains)
								.Self()
								.UnsafeInline();
					})
					.DefaultSources(options =>
					{
						options.CustomSources(cspTrustedDomains)
								.Self();
					});
			}
			else
			{
				csp.ImageSources(options =>
				{
					options.CustomSources("data:")
							.Self();
				});
			}

			AddTrustedConnectDomains(csp, configuration);
		});
		return thisValue;
	}

	[Conditional("DEBUG")]
	private static void AddTrustedConnectDomains([NotNull] IFluentCspOptions csp, [NotNull] IConfiguration configuration)
	{
		string[] cspTrustedConnectDomains = configuration.GetSection(CSP_TRUSTED_CONNECT_DOMAINS_KEY).Get<string[]>();
		if (cspTrustedConnectDomains is not { Length: > 0 }) return;
		csp.ConnectSources(options =>
		{
			options.CustomSources(cspTrustedConnectDomains)
					.Self();
		});
	}
}