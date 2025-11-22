# Bug Report

## Bug Description

The `ExecuteFile` method in `Core/Scripting.cs` does not handle `null` file paths. If a `null` value is passed as the `filePath` argument, a `NullReferenceException` is thrown when `File.Exists` is called. The expected behavior is to throw an `ArgumentNullException`.

## Fix

The fix is to add a `null` check at the beginning of the `ExecuteFile` method and throw an `ArgumentNullException` if the `filePath` is `null`.
