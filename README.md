# LeviLog

A customizable logger with a web based front end

## Features

- Customizable index page
- Customizable log pages
- Thread Safe
- Web based front end

## Usage

### Creating a new logger

There are currently 3 builtin loggers, `LoggerBase`, `WebConsole`, and `WebDocument`.

To create a new logger you must inherit from one of these three.

#### LoggerBase

The LoggerBase is the base class for all loggers. It provides the basic functionality for a logger.

You must provide implementation for the HTML method and the Encode function.

The HTML method should return the HTML for the log page.

There are some auto replace strings which you can use when creating the HTML.

- `$LEVILOGGER_LOGGER_PORT$` - The port the logger is running on
- `$LEVILOGGER_LOGGER_NAME$` - The name of the logger
- `$LEVILOGGER_SESSION_ID$` - The session id of the logger

The Encode function should return the encoded string of the log message.

This is used to encode the log message before it is sent to the client.

As input it takes an array of objects and you control how the objects should be displayed.

Check the implementation of the WebConsole and WebDocument for examples.

### WebConsole

The WebConsole is a logger that logs to the web browser's console.

For example in Chrome you can open the console by pressing `F12` and then clicking on the console tab.

```cs
using static LeviLog; // Add this where ever you want to use LeviLog

public class TestWebLogger : WebConsole; // Create a new logger that inherits from WebConsole

public class Test
{
	static void Main()
	{
		InitLeviLog(); // Initialize LeviLog with the default settings

		// example
		while (true)
		{
			Log<TestWebLogger>("Hello World!"); // Log a message to the logger
			await Task.Delay(1000);
		}
	}
}
```

### WebDocument

The WebDocument is a logger that logs to a web page.

The built in logger uses a simple HTML page to display the logs.

```cs
using static LeviLog; // Add this where ever you want to use LeviLog

public class TestWebLogger : WebDocument; // Create a new logger that inherits from WebDocument

public class Test
{
	static void Main()
	{
		InitLeviLog(); // Initialize LeviLog with the default settings

		// example
		while (true)
		{
			Log<TestWebLogger>("Hello World!"); // Log a message to the logger
			await Task.Delay(1000);
		}
	}
}
```

### LoggerBase

The LoggerBase is the base class for all loggers and must be inherited from.

It does not provide any functionality and must be implemented.

To get an idea on how to implement LoggerBase check the implementation of WebConsole and WebDocument.

## Limitations

- The logger was desgin to only show logs when active and so when not active log messages are discarded. This has the affect of not being able to see logs that were created while the logger isn't active.
- The logger is not designed for high performance logging but more for flexibility and ease of use so when logging too fast the logger may not be able to keep up.

### To Do

- Add more built in loggers
- Add more customization options
- Add the ability to enforce log messages by waiting until a log message is sent
