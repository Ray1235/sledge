﻿using System.ComponentModel.Composition.Hosting;
using System.Threading.Tasks;

namespace Sledge.Common.Hooks
{
    /// <summary>
    /// A hook that runs just after startup
    /// </summary>
    public interface IInitialiseHook
    {
        /// <summary>
        /// Runs when initialised, after startup
        /// </summary>
        /// <returns></returns>
        Task OnInitialise();
    }
}