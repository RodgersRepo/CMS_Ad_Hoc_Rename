/*
Copyright (C) 2024  Rodge Industries 2000
 
     This program is free software: you can redistribute it and/or modify
     it under the terms of the GNU General Public License as published by
     the Free Software Foundation, version 3.
 
     This program is distributed in the hope that it will be useful,
     but WITHOUT ANY WARRANTY; without even the implied warranty of
     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
     GNU General Public License for more details.
 
     To view the GNU General Public License, see <http://www.gnu.org/licenses/>.
     A copy of the LICENSE can be found in this repository.
    Release Date: 04/12/2024
    Last Updated:      
   
    Change comments:
    Initial realease V1 - RITT   
   
  Author: RodgeIndustries2000 (RITT) with help from Microsoft CoPilot
*/

///////////////////////////////////////////////////////////////////////
// Using Directives                                                  //
// ----------------                                                  //
// which namespaces to look in to find the classes used in this code.//
// Classes serve as blueprints or templates that defines the         //
// properties (state) and methods (behavior) that the objects        //
// created from the class will have.                                 //
///////////////////////////////////////////////////////////////////////

using Microsoft.AspNetCore.DataProtection;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Net.Http.Headers;
using Serilog;

///////////////////////////////////////////////////////////////////////
// Top-level Statements                                              //
// --------------------                                              //
///////////////////////////////////////////////////////////////////////

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Create a new object from the LoggerService class to log to a text
// file. Calls LoggerService class which uses Serilog directive.
// An object is an instance of a class
var loggerService = new LoggerService();

///////////////////////////////////////////////////////////////////////
// HTTP route handler methods                                        //
// --------------------------                                        //
///////////////////////////////////////////////////////////////////////

// What to do if a Get is recieved looking
// for instructions. Route to the index.html file
app.MapGet("/index.html", async (HttpContext context) => 
{
    context.Response.ContentType="text/html";
    await context.Response.Body.WriteAsync(File.ReadAllBytes("index.html"));
}
);

// What to do if a GET recieved testing http availability of this web app
app.MapGet("/", async () => await Task.FromResult(Environment.MachineName + " cmsrename app is responding to GET requests"));

//What to do if a POST is recieved to set up encrypted credentials
app.MapPost("/creds", async (HttpContext context, HttpRequest request, IWebHostEnvironment env) => 
{
    string computerName = Environment.MachineName;
    // Check results is not empty
    // Ensure the request content type is x-www-form-urlencoded
    if (context.Request.HasFormContentType)
    {
        var form = await context.Request.ReadFormAsync();
        var userName = form["uname"].ToString();
        //encrypt the password for storage. Use the class
        //below
        var encryptor = new EncryptionService();
        // Encrypt the string and store it as a SecureString
        //Alternaivly store as an encrypted string
        string encryptedString = encryptor.Encrypt(form["psw"].ToString());

        if (!string.IsNullOrEmpty(userName))
        {
            //File path where creds.csv will be saved.User must manually
            //create this CSV file otherwise insecure default credentials will be used.
            var filePath =  env.ContentRootPath + "\\private\\";
            // Create the directory for the creds CSV if it dosnt already exsist
            Directory.CreateDirectory(filePath);

            // Read the contents of creds.html 
            var htmlContent = await File.ReadAllTextAsync("creds.html");
            // Replace the placeholedr text witin creds.html
            // with the actual variable contents recieved in the POST
            htmlContent = htmlContent.Replace("{{userName}}", userName)
                             .Replace("{{computerName}}", computerName)
                             .Replace("{{filePath}}", filePath)
                             .Replace("{{encryptedString}}", encryptedString);
            
            // Send the updated creds.html to the users browser
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(htmlContent);            
        }
    }
}
);

//What to do if a POST is recieved to turn logging on or off
app.MapPost("/logging", async (HttpContext context, HttpRequest request, IWebHostEnvironment env) => 
{
    // Check results is not empty
    // Ensure the request content type is x-www-form-urlencoded
    if (context.Request.HasFormContentType)
    {
        var form = await context.Request.ReadFormAsync();
        GlobalSetLogging.turnOnLog = form["option"].ToString();   
        loggerService.LogMessage($"Logging has been set to: {GlobalSetLogging.turnOnLog}. True means logging is on. False is no logging"); 

        // Read the contents of creds.html 
        var htmlContent = await File.ReadAllTextAsync("logging.html");
        // Replace the placeholedr text witin logging.html
        // with the actual variable contents recieved in the POST
        htmlContent = htmlContent.Replace("{{turnOnLog}}", GlobalSetLogging.turnOnLog)
                             .Replace("{{logFile}}", GlobalSetLogging.logFile);
            
        // Send the updated creds.html to the users browser
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(htmlContent);       
    }
}
);

// Post recieved from cms with call detail records for a new
// ad hoc space. Call methods from the methods section.
app.MapPost("/", async (HttpContext context, HttpRequest request, IWebHostEnvironment env) =>
{
    var (spaceGuid, spaceName) = await ReadRequestBodyAsync(request);
    

    if (spaceGuid.Value != "notFound" && spaceName.Value != "notFound")
    {
        LogMessage($"A CMS coSpace string named {spaceName.Value} received. accompaning GUID is {spaceGuid.Value}.");
        var credentials = await GetCredentials(env.ContentRootPath + "\\private\\creds.csv");
        var byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{credentials.Password}");
        

        if (credentials is { SpaceName: not null, Username: not null, Password: not null, ConfPrefix: not null } && spaceName.Value.StartsWith(credentials.ConfPrefix))
        {
                var encryptionService = new EncryptionService();
            try
            {
                encryptionService.Decrypt(credentials.Password);
                LogMessage("An encrypted password has been set. This will be used to log into CMS");
                byteArray = Encoding.ASCII.GetBytes($"{credentials.Username}:{encryptionService.Decrypt(credentials.Password)}");
            }
            catch
            {
                // Use plain
                // language password
                LogMessage($"UNABLE TO DECRYPT PASSWORD using the insecure plain language password of: {credentials.Password}");
            }

            // Send the name change to CMS using method SendRequestAsync
            await SendRequestAsync(spaceGuid.Value, context, credentials.SpaceName, byteArray);
        }
        else
        {
            LogMessage($"{spaceName.Value} received does not begin with the configured prefix and will be ignored.");
        }
    }
});

// Start the application and keep it running until it is manually stopped
app.Run();
// Flush and close the text log when the application exits
loggerService.CloseAndFlush();

///////////////////////////////////////////////////////////////////////
// All other Methods                                                 //
// -----------------                                                 //
// a method is a block of code that performs a specific task and can //
// be called from other parts of the program, sounds a lot like      //
// functions but methods are tied to an object or class and are used //
// in object-oriented programming                                    //
///////////////////////////////////////////////////////////////////////

//  A method for sending the HTTP PUT request to CMS
async Task SendRequestAsync(string spaceGuidValue, HttpContext context, string spaceName, byte[] byteArray)
{
    // Bypass checking the CMS certificate
    var handler = new HttpClientHandler{
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    };
    using var client = new HttpClient(handler);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
    
    

    string jsonContent = $"name={spaceName}";
    var content = new StringContent(jsonContent, Encoding.UTF8, "application/x-www-form-urlencoded");
    
    var response = await client.PutAsync($"https://{context.Connection.RemoteIpAddress}:443/api/v1/coSpaces/{spaceGuidValue}", content);
    
    LogMessage($"sending to https://{context.Connection.RemoteIpAddress}:443/api/v1/coSpaces/{spaceGuidValue} with {jsonContent}");
    
    var statusCode = response.StatusCode;
    var responseContent = await response.Content.ReadAsStringAsync();
    
    LogMessage($"status code {statusCode} {responseContent}");
}

// A method that reads the returned xml and finds the
// values for coSpace and space name. If these are not found 
// assigns "notFound" to these. Called by the method app.MapPost "/"
// which will test for "notFound"
async Task<(XElement coSpace, XElement spaceName)> ReadRequestBodyAsync(HttpRequest request)
{
    using (var reader = new System.IO.StreamReader(request.Body))
    {
        string xml = await reader.ReadToEndAsync();
        var xdoc = XDocument.Parse(xml);
        var coSpace = xdoc.XPathSelectElement("//coSpace") ?? new XElement("coSpace", "notFound");
        var spaceName = xdoc.XPathSelectElement("//name") ?? new XElement("name", "notFound");

        return (coSpace, spaceName);
    }
}

// Method creates/waits/returns new Credentials instance
async Task<Credentials> GetCredentials(string filePath)
{
    var credentialsReader = new CredentialsReader();
    return await credentialsReader.GetCredentials(filePath);
}

// Method. Void as returns no values. Writes your message
// to both the console and loging service.
// GlobalSetLogging.turnOnLog must ne set true
// to write to the text log in 
// bin\Debug\net*.*\cmsRenameLogYYYMMDD.txt
void LogMessage(string message)
{
    Console.WriteLine(message);
    loggerService.LogMessage(message);
}

///////////////////////////////////////////////////////////////////////
// Records and classes                                               //
// -------------------                                               //
// Records and classes have to be defined after Top-level statements //
// records are immutable, cant add new properties once defined       //
// string? means it can be empty or optional                         //
///////////////////////////////////////////////////////////////////////

// A class for holding creds and data
// this is an example of a Data Transfer Object class
// no code just data, in this case default settings.
// Used by CredentialsReader class
public class Credentials
{
    public string? Username { get; set; } = "cmsrename";
    public string? Password { get; set; } = "insecure";
    public string? SpaceName { get; set; } = "Ad Hoc Conference";
    public string? ConfPrefix { get; set; } = "123456";
}

// Same as above but static i.e
// no need to maintain state or be instantiated.
// Settings for the LoggerService class.
public static class GlobalSetLogging
{
    public static string turnOnLog { get; set; } = "false";
    public static string logFile { get; set; } = AppContext.BaseDirectory + "Logs\\cmsRenameLog.txt";
}

// A class that starts a logging service to a text file.
// Events are only logged if GlobalSetLogging.logFile is set to true
// uses the directive Serilog
public class LoggerService
{
    public LoggerService()
    {
        string logFile = GlobalSetLogging.logFile;
	        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true)
            .CreateLogger();
    }

    public void LogMessage(string message)
    {
        
        if(GlobalSetLogging.turnOnLog == "true")
	    {
		    Log.Information(message);
	    }
	
    }

    public void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}

// A class that reads in the credentials and other data this app needs to make a change
// to the CMS space name. If no data is provided by the creds.csv file defaults
// are used
public class CredentialsReader
{
    // The task here returns a Data Transfer Object of type Credentials
    // which is defined earlier, see line 430
    public async Task<Credentials> GetCredentials(string filePath)
    {
        // New instance of the Credentials Data Transfer Object class
        // using default settings 
        var defaultCredentials = new Credentials();

        if (!File.Exists(filePath))
        {
            return defaultCredentials;
        }

        using (var reader = new StreamReader(filePath))
        {
            // If user has created the CSV file /private/creds.csv
            // read/validate the values in that and use.
            // Skip the first line as this is just the column headings
            await reader.ReadLineAsync();

            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                return defaultCredentials;
            }

            var parts = line.Split(',');
            if (parts.Length < 4)
            {
                return defaultCredentials;
            }

            var credsFromCsv = new Credentials
            {
                Username = parts[0],
                Password = parts[1],
                SpaceName = parts[2],
                ConfPrefix = parts[3]
            };

            return credsFromCsv;
        }
    }
}

// A class that can encrypt/decrypt strings. Used to encrypt/decrypt the CMS
// password this web app will use to access the CMS API.
public class EncryptionService
{
    private readonly IDataProtector protector;

    public EncryptionService()
    {
        var provider = DataProtectionProvider.Create("MyApp");
        protector = provider.CreateProtector("MyPurpose");
    }

    public string Encrypt(string plainText)
    {
        // Return the encrypted string
        return protector.Protect(plainText);
    }

    public string Decrypt(string encryptedText)
    {
        // Return the decrypted string
        return protector.Unprotect(encryptedText);
    }
}

