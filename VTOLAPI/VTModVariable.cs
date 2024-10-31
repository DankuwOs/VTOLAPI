using System;

namespace VTOLAPI;


public class VTModVariable
{
    [Flags]
    public enum VariableAccess
    {
        Getter = 1,
        Setter = 2, // I do not know what the purpose of only a setter would be but here you go i guess
        Both = 3
    }
    
    public delegate void ActionRef<T>(ref T item);
    
    /// <param name="variableName">Unique name of the variable, used as a key.</param>
    /// <param name="initialValue">Initial value, must be the same type as the variable.</param>
    /// <param name="onSetValue">Action invoked when another mod wants to set your variable, set your value to the parameter.</param>
    /// <param name="onGetValue">Action invoked when another mod wants your value, set the parameter to your value.</param>
    /// <param name="variableAccess">Determines what other mods can do with your variable.</param>
    /// <code>
    /// float epicFloat = 420.69f;
    /// VTModVariable modVariable = new VTModVariable("Epic Float Variable", epicFloat, SetValue, GetValue);
    ///  
    /// void SetValue(object value) {
    ///     epicFloat = (float)value; // Value is type checked.
    /// }
    /// void GetValue(ref object value) {
    ///     value = epicFloat;
    /// }
    /// </code>
    public VTModVariable(string variableName, object initialValue, Action<object> onSetValue, ActionRef<object> onGetValue, VariableAccess variableAccess = VariableAccess.Both)
    {
        this.variableName = variableName;
        _value = initialValue;
        _valueType = initialValue.GetType();

        OnSetValue = onSetValue;
        OnGetValue = onGetValue;

        _variableAccess = variableAccess;
    }

    /// <summary>
    /// Action based variablen't, can be used to allow other mods to invoke events in yours.
    /// </summary>
    /// <param name="variableName">Unique name of the variablen't, used as a key.</param>
    /// <param name="onInvoke">Action invoked when another mod wants you to do something.</param>
    /// <code>
    /// OnShoot = new Action(Shoot);
    /// VTModVariable modVariable = new VTModVariable("OnShoot", OnShoot);
    ///  
    /// void Shoot() {
    ///     // Shoots :~)
    /// }
    /// </code>
    public VTModVariable(string variableName, Action onInvoke)
    {
        this.variableName = variableName;
        _isAction = true;

        OnInvoke = onInvoke;
    }

    public string variableName;
    
    private object _value;
    private Type _valueType;

    private VariableAccess _variableAccess;

    private bool _isAction;

    private Action<object> OnSetValue;
    private ActionRef<object> OnGetValue;
    private Action OnInvoke;

    
    public bool TryGetValue(out object outValue)
    {
        if (!_variableAccess.HasFlag(VariableAccess.Getter))
        {
            LogError($"Tried to get value for '{variableName}' but the variable access is '{_variableAccess}'!");
        }
        if (_isAction)
        {
            LogError($"Tried to get value for '{variableName}' but its an action!");
            outValue = null;
            return false;
        }
        
        OnGetValue?.Invoke(ref _value);
        outValue = _value;
        
        return _value != null;
    }

    public bool TrySetValue(object value)
    {
        if (!_variableAccess.HasFlag(VariableAccess.Setter))
        {
            LogError($"Tried to set value for '{variableName}' but the variable access is '{_variableAccess}'!");
        }
        if (_isAction)
        {
            LogError($"Tried to set value for '{variableName}' but its an action!");
            return false;
        }
        if (value.GetType() != _valueType)
        {
            LogError($"Tried to set value for '{_valueType}::{variableName}' but the type is '{value.GetType()}'!");
            return false;
        }
        
        _value = value;
        
        OnSetValue?.Invoke(_value);
        return true;
    }

    public void Invoke()
    {
        if (_isAction)
            OnInvoke?.Invoke();
        else
        {
            LogWarn($"Tried to invoke a non action for '{_valueType}::{variableName}'");
        }
    }
}