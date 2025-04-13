using System;
using System.Windows.Input;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class RelayCommand : ICommand
	{
		private readonly Action _execute;
		private readonly Func<bool>? _canExecute;

		public RelayCommand(Action execute, Func<bool>? canExecute = null)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

		public void Execute(object? parameter) => _execute();

		public event EventHandler? CanExecuteChanged
		{
			add => CommandManager.RequerySuggested += value;
			remove => CommandManager.RequerySuggested -= value;
		}
	}

	public class LoginPlayer<T> : ICommand
	{
		private readonly Action<T> _execute;
		private readonly Predicate<T>? _canExecute;

		public LoginPlayer(Action<T> execute, Predicate<T>? canExecute = null)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public bool CanExecute(object? parameter) =>
			_canExecute?.Invoke((T)parameter!) ?? true;

		public void Execute(object? parameter) =>
			_execute((T)parameter!);

		public event EventHandler? CanExecuteChanged
		{
			add => CommandManager.RequerySuggested += value;
			remove => CommandManager.RequerySuggested -= value;
		}
	}
}
