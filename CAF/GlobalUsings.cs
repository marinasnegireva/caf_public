// Global using directives for CAF project

global using CAF.DB;
global using CAF.DB.Entities;
global using CAF.Interfaces;
global using CAF.LLM.Claude;
global using CAF.LLM.Gemini;
global using CAF.Controllers.Models.Requests;
global using CAF.Controllers.Models.Responses;
global using CAF.Services;
global using CAF.Services.Telegram;
global using CAF.Services.VectorDB;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Options;
global using Serilog;
global using System.Collections.Concurrent;
global using System.Text;
global using System.Text.Encodings.Web;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;
global using CAF.Services.Conversation.Enrichment.Enrichers;