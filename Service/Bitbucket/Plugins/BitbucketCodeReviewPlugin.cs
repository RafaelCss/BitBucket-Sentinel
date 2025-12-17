using Bitbucket_PR_Sentinel.Contratos;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace Bitbucket_PR_Sentinel.Service.Bitbucket.Plugins;

public class BitbucketCodeReviewPlugin
{
    private readonly IBitbucketService _bitbucket;
    private readonly IConfiguration _config;
    private readonly ILogger<BitbucketCodeReviewPlugin> _logger;
    private readonly string _workspace;
    private readonly string _repo;

    public BitbucketCodeReviewPlugin(
        IBitbucketService bitbucket ,
        IConfiguration config ,
        ILogger<BitbucketCodeReviewPlugin> logger)
    {
        _bitbucket = bitbucket;
        _config = config;
        _logger = logger;

        _workspace = _config["Bitbucket:Workspace"]
            ?? throw new InvalidOperationException("Bitbucket:Workspace não encontrado");
        _repo = _config["Bitbucket:Repo"]
            ?? throw new InvalidOperationException("Bitbucket:Repo não encontrado");
    }

}
