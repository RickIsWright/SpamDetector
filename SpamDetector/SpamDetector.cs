﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Piranha;
using Piranha.Models;
using Piranha.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Zon3.SpamDetector
{
    /// <summary>
    /// Designed for https://akismet.com/
    /// 
    /// Briefly considered anti-spam SaaS:
    ///     https://cleantalk.org
    ///     https://www.oopspam.com/
    /// </summary>
    public abstract class SpamDetector : ISpamDetector
    {
        protected IApi _piranha;

        protected ILogger _logger;

        protected IHttpClientFactory _httpClientFactory;

        protected SpamDetectorOptions _options;

        protected Guid _commentId;

        public bool Enabled => _options.Enabled;

        public SpamDetector(IApi piranhaApi, IOptions<SpamDetectorOptions> options, IHttpClientFactory clientFactory, ILoggerFactory logger)
        {
            _piranha = piranhaApi;
            _httpClientFactory = clientFactory;
            _options = options.Value;

            if (logger != null)
            {
                _logger = logger.CreateLogger(this.GetType().FullName);
            }

            if (string.IsNullOrEmpty(_options.SpamApiUrl))
            {
                var msg = $"Option SpamApiUrl is missing: value mandatory";
                _logger.LogError(msg);
                throw new InvalidOperationException(msg);
            }

            if (string.IsNullOrEmpty(_options.SiteUrl))
            {
                _logger.LogWarning("Option SiteUrl is missing: results may be wrong");
            }

            if (_options.IsTest)
            {
                _logger.LogWarning("Option IsTest is true: no live requests will be made");
            }
        }

        public async Task<CommentReview> ReviewAsync(Comment comment)
        {
            _commentId = comment.Id;

            // If not enabled, warn and use existing value for comment  
            if (!Enabled)
            {
                var msg = $"Option Enabled is false: no review for comment '{_commentId}' made";
                _logger.LogWarning(msg);

                return new CommentReview() { Approved = comment.IsApproved, Information = msg };
            }

            // Get a client for API request
            var client = _httpClientFactory.CreateClient();

            // Create a request with relevant parameters added from comment
            var requestMessage = await GetSpamRequestMessageAsync(comment);

            // Send request and make sure we have a valid response
            var response = await client.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            // Get review result from response
            var review = await GetCommentReviewFromResponse(response);

            // Review done
            return review;
        }

        // Leave implementation to derived class
        protected abstract Task<HttpRequestMessage> GetSpamRequestMessageAsync(Comment comment);

        // Leave implementation to derived class
        protected abstract Task<CommentReview> GetCommentReviewFromResponse(HttpResponseMessage response);
    }
}
