using System.Collections.Generic;
using UnityEngine;

namespace VTOLAPI;

public class VTModVariables
{
    public VTModVariables(string modId)
    {
        _modVariables ??= new Dictionary<string, VTModVariable>();
        this.modId = modId;
    }
    
    public bool Unregistered { get; internal set; }

    private string modId;

    /// <param name="Key">Variable Name</param>
    /// <param name="Value">VTModVariable</param>
    private Dictionary<string, VTModVariable> _modVariables = new Dictionary<string, VTModVariable>();


    internal void RegisterVariable(VTModVariable modVariable)
    {
        Log($"Registering variable '{modId}:{modVariable.variableName}'");
        if (_modVariables.ContainsKey(modVariable.variableName))
        {
            LogWarn($"Tried to register variable '{modId}:{modVariable.variableName}' but it's already been registered.");
            return;
        }
        _modVariables.Add(modVariable.variableName, modVariable);
    }

    internal void UnregisterVariable(string variableName)
    {
        Log($"Unregistering variable '{modId}:{variableName}'");
        if (_modVariables.ContainsKey(variableName))
        {
            _modVariables.Remove(variableName);
        }
        else
        {
            LogWarn($"Tried to unregister variable but '{modId}:{variableName}' doesn't exist?");
        }
    }

    /// <param name="variableName">Unique name of the variable to get, used as a key.</param>
    /// <param name="value">THE value.</param>
    /// <returns>True if variable is accessible, not an action, and value isn't null.</returns>
    public bool TryGetValue(string variableName, out object value)
    {
        Log($"Trying to get variable '{modId}:{variableName}'");
        value = null;
        
        if (Unregistered)
        {
            LogWarn($"Tried to get value of unregistered mod '{modId}:{variableName}'");
            return false;
        }
        
        if (_modVariables.TryGetValue(variableName, out var modVariable))
        {
            if (modVariable.TryGetValue(out value))
            {
                Log($"Got variable '{modId}:{variableName}={value}'");
                return true;
            }

            return false;
        }

        LogWarn($"Tried to get variable '{modId}:{variableName}' but _modVariables doesn't contain it.");
        return false;
    }

    /// <param name="variableName">Unique name of the variable to set, used as a key.</param>
    /// <param name="value">THE value.</param>
    /// <returns>True if variable is accessible, not an action, and value is the same type.</returns>
    public bool TrySetValue(string variableName, object value)
    {
        if (Unregistered)
        {
            LogWarn($"Tried to set value of unregistered mod '{modId}:{variableName}'");
            return false;
        }
        
        Log($"Trying to set variable '{modId}:{variableName}' to '{value}'");
        if (_modVariables.TryGetValue(variableName, out var modVariable))
        {
            if (modVariable.TrySetValue(value))
            {
                Log($"Set variable '{modId}:{variableName}={value}'");
                return true;
            }

            return false;
        }

        LogWarn($"Tried to set variable '{modId}:{variableName}' but _modVariables doesn't contain it.");
        return false;
    }

    /// <summary>
    /// Invokes an action used by the target mod to do something.
    /// </summary>
    /// <param name="variableName">Unique name of the variable to invoke, used as a key.</param>
    public void Invoke(string variableName)
    {
        if (Unregistered)
        {
            LogWarn($"Tried to invoke variable of unregistered mod '{modId}:{variableName}'");
            return;
        }
        
        Log($"Trying to invoke variable '{modId}:{variableName}'");
        if (_modVariables.TryGetValue(variableName, out var modVariable))
        {
            modVariable.Invoke();
            return;
        }

        LogWarn($"Tried to invoke variable '{modId}:{variableName}' but _modVariables doesn't contain it.");
    }
}