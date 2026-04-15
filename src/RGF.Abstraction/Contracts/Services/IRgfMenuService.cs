using Recrovit.RecroGridFramework.Abstraction.Infrastructure.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.Services;

public interface IRgfMenuService
{
    Task<IRgfApiResponse<List<RgfMenu>>> GetMenuAsync(int menuId, string lang, string scope = null);
}
