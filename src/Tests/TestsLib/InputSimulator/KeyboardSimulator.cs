﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xpand.EasyTest.Automation.InputSimulator;
using Xpand.TestsLib.Win32;

namespace Xpand.TestsLib.InputSimulator{
    public class KeyboardSimulator : IKeyboardSimulator{
        private readonly IInputSimulator _inputSimulator;
        private readonly IInputMessageDispatcher _messageDispatcher;

        public KeyboardSimulator(IInputSimulator inputSimulator){
	        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
            _messageDispatcher = new WindowsInputMessageDispatcher();
        }

        internal KeyboardSimulator(IInputSimulator inputSimulator,
            IInputMessageDispatcher messageDispatcher){
	        _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
            _messageDispatcher = messageDispatcher ?? throw new InvalidOperationException(
	            string.Format(
		            "The {0} cannot operate with a null {1}. Please provide a valid {1} instance to use for dispatching {2} messages.",
		            nameof(KeyboardSimulator), nameof(IInputMessageDispatcher),
		            nameof(Win32Types.INPUT)));
        }

        public IMouseSimulator Mouse => _inputSimulator.Mouse;

        public IKeyboardSimulator KeyDown(Win32Constants.VirtualKeys keyCode){
            Win32Types.INPUT[] inputList = new InputBuilder().AddKeyDown(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator KeyUp(Win32Constants.VirtualKeys keyCode){
            Win32Types.INPUT[] inputList = new InputBuilder().AddKeyUp(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator KeyPress(Win32Constants.VirtualKeys keyCode){
            Win32Types.INPUT[] inputList = new InputBuilder().AddKeyPress(keyCode).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator KeyPress(params Win32Constants.VirtualKeys[] keyCodes){
            var builder = new InputBuilder();
            KeysPress(builder, keyCodes);
            SendSimulatedInput(builder.ToArray());
            return this;
        }

        public IKeyboardSimulator ModifiedKeyStroke(Win32Constants.VirtualKeys modifierKeyCode,
            Win32Constants.VirtualKeys keyCode){
            ModifiedKeyStroke(new[]{modifierKeyCode}, new[]{keyCode});
            return this;
        }

        public IKeyboardSimulator ModifiedKeyStroke(
            IEnumerable<Win32Constants.VirtualKeys> modifierKeyCodes, Win32Constants.VirtualKeys keyCode){
            ModifiedKeyStroke(modifierKeyCodes, new[]{keyCode});
            return this;
        }

        public IKeyboardSimulator ModifiedKeyStroke(Win32Constants.VirtualKeys modifierKey,
            IEnumerable<Win32Constants.VirtualKeys> keyCodes){
            ModifiedKeyStroke(new[]{modifierKey}, keyCodes);
            return this;
        }

        public IKeyboardSimulator ModifiedKeyStroke(
            IEnumerable<Win32Constants.VirtualKeys> modifierKeyCodes, IEnumerable<Win32Constants.VirtualKeys> keyCodes){
            var builder = new InputBuilder();
            Win32Constants.VirtualKeys[] virtualKeyss = modifierKeyCodes.ToArray();
            ModifiersDown(builder, virtualKeyss);
            KeysPress(builder, keyCodes);
            ModifiersUp(builder, virtualKeyss);

            SendSimulatedInput(builder.ToArray());
            return this;
        }

        public IKeyboardSimulator TextEntry(string text){
            if (text.Length > UInt32.MaxValue/2)
                throw new ArgumentException(
	                $"The text parameter is too long. It must be less than {UInt32.MaxValue / 2} characters.", nameof(text));
            Win32Types.INPUT[] inputList = new InputBuilder().AddCharacters(text).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator TextEntry(char character){
            Win32Types.INPUT[] inputList = new InputBuilder().AddCharacter(character).ToArray();
            SendSimulatedInput(inputList);
            return this;
        }

        public IKeyboardSimulator Sleep(int millsecondsTimeout){
            Thread.Sleep(millsecondsTimeout);
            return this;
        }

        public IKeyboardSimulator Sleep(TimeSpan timeout){
            Thread.Sleep(timeout);
            return this;
        }

        private void ModifiersDown(InputBuilder builder, IEnumerable<Win32Constants.VirtualKeys> modifierKeyCodes){
            if (modifierKeyCodes == null) return;
            foreach (Win32Constants.VirtualKeys key in modifierKeyCodes) builder.AddKeyDown(key);
        }

        private void ModifiersUp(InputBuilder builder, IEnumerable<Win32Constants.VirtualKeys> modifierKeyCodes){
            if (modifierKeyCodes == null) return;

            var stack = new Stack<Win32Constants.VirtualKeys>(modifierKeyCodes);
            while (stack.Count > 0) builder.AddKeyUp(stack.Pop());
        }

        private void KeysPress(InputBuilder builder, IEnumerable<Win32Constants.VirtualKeys> keyCodes){
            if (keyCodes == null) return;
            foreach (Win32Constants.VirtualKeys key in keyCodes) builder.AddKeyPress(key);
        }

        private void SendSimulatedInput(Win32Types.INPUT[] inputList){
            _messageDispatcher.DispatchInput(inputList);
        }
    }
}