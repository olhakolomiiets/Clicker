using CBS.Models;
using System;

namespace CBS
{
    public interface IRoulette
    {
        /// <summary>
        /// Get list of all roulette positions
        /// </summary>
        /// <param name="result"></param>
        void GetRouletteTable(Action<CBSGetRouletteTableResult> result);

        /// <summary>
        /// Start spin roulette and get spin result
        /// </summary>
        /// <param name="result"></param>
        void Spin(Action<CBSSpinRouletteResult> result);
    }
}
