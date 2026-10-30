using Microsoft.AspNetCore.Mvc;
using Quorum.App.Features.Shared;

namespace Quorum.App.Features.Groups;

public sealed class GroupNewModel : SignedInPageModel
{
    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public string DisplayName { get; set; } = "";

    public void OnGet()
    {
    }
}
