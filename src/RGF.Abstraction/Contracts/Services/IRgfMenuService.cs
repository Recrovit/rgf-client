using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

/// <summary>Loads backend menu definitions for the current user and requested language.</summary>
public interface IRgfMenuService
{
    /// <summary>Gets a menu through the configured RGF API client.</summary>
    /// <param name="menuId">Backend menu identifier.</param>
    /// <param name="lang">Requested language identifier.</param>
    /// <param name="scope">Optional backend menu scope.</param>
    /// <returns>The HTTP envelope containing menu entries when successful.</returns>
    Task<IRgfApiResponse<List<RgfMenu>>> GetMenuAsync(int menuId, string lang, string scope = null);
}
