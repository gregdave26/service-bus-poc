// The Digital Site tab calls the .NET CommerceApi directly (CORS); this server only tells the browser where it is.
export const digitalSiteClientConfig = {
  digitalSiteApiBaseUrl: process.env.DIGITAL_SITE_API_BASE_URL ?? "http://localhost:5200",
};
