using System;
using System.Collections.Generic;
using UnityEngine.LowLevel;

namespace Reflow
{
    internal static class ReflowPlayerLoopUtil
    {
        /// <summary>
        /// Puts a system of type <paramref name="pSystemType"/> right before <paramref name="pAnchorType"/> inside the
        /// <paramref name="pPhaseType"/> phase, replacing an earlier copy. Returns false when the anchor is missing.
        /// </summary>
        internal static bool InsertBefore(ref PlayerLoopSystem pRoot, Type pPhaseType, Type pAnchorType, Type pSystemType, PlayerLoopSystem.UpdateFunction pFunction)
        {
            return Edit(ref pRoot, pPhaseType, pAnchorType, pSystemType, pFunction);
        }

        /// <summary> Appends a system at the end of <paramref name="pPhaseType"/>, replacing an earlier copy. </summary>
        internal static bool Append(ref PlayerLoopSystem pRoot, Type pPhaseType, Type pSystemType, PlayerLoopSystem.UpdateFunction pFunction)
        {
            return Edit(ref pRoot, pPhaseType, null, pSystemType, pFunction);
        }

        private static bool Edit(ref PlayerLoopSystem pRoot, Type pPhaseType, Type pAnchorType, Type pSystemType, PlayerLoopSystem.UpdateFunction pFunction)
        {
            PlayerLoopSystem[] phaseArray = pRoot.subSystemList;
            if (phaseArray == null)
                return false;

            for (int p = 0; p < phaseArray.Length; p++)
            {
                if (phaseArray[p].type != pPhaseType)
                    continue;

                PlayerLoopSystem[] oldArray = phaseArray[p].subSystemList ?? Array.Empty<PlayerLoopSystem>();
                List<PlayerLoopSystem> systemList = new List<PlayerLoopSystem>(oldArray.Length + 1);
                for (int i = 0; i < oldArray.Length; i++)
                {
                    if (oldArray[i].type != pSystemType)
                        systemList.Add(oldArray[i]);
                }

                int insertAt = systemList.Count;
                if (pAnchorType != null)
                {
                    insertAt = systemList.FindIndex(pSystem => pSystem.type == pAnchorType);
                    if (insertAt < 0)
                        return false;
                }

                systemList.Insert(insertAt, new PlayerLoopSystem { type = pSystemType, updateDelegate = pFunction });
                phaseArray[p].subSystemList = systemList.ToArray();
                return true;
            }
            return false;
        }
    }
}
