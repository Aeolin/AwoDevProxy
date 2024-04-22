// See https://aka.ms/new-console-template for more information
using AwoDevProxy.Shared;
using AwoDevProxy.Shared.Messages;
using AwoDevProxy.Web.Api.Service.Cookies;
using System.Security.Cryptography;

var config = new CookieConfig { FingerPrint = "DEVPRXY!", SigningIV = "GM6qudkIsv8U6W173xDPTA==", SigningKey = "2^F$9y#GuHfT!sf8H%bapZU9oTR7#Pj7" };
var cookieService = new CookieService(config);

var proxyFingerprint = new byte[16];

var cookie = cookieService.CreateCookie(proxyFingerprint);
Console.WriteLine(cookie);
var valid = cookieService.IsValid(cookie, proxyFingerprint);
var sizes = AesCng.Create().LegalBlockSizes;
Console.WriteLine($"Cookie {cookie} is valid: {valid}");