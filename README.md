# MinecraftProtoNet

MinecraftProtoNet is a high-performance, modular .NET 10 framework for building advanced Minecraft clients, autonomous bots, and automated tools. Engineered from the ground up using modern C# 14 features, it provides a robust foundation for interacting with Minecraft servers at the protocol level, without requiring a standard game client.

## ✨ Core Capabilities & Features

The project is divided into specialized modules, offering a comprehensive suite of tools for automation, pathfinding, trading, and monitoring.

### 🔌 Protocol & Engine (`MinecraftProtoNet.Core` & `MinecraftProtoNet.Core.NBT`)
- **Headless Client Engine:** Connects and interacts with Minecraft servers natively via TCP, handling encryption, compression, and keep-alives automatically.
- **Full World State Tracking:** Parses and maintains real-time data for chunks, block states, entities, and tile entities.
- **Physics Simulation:** Built-in client-side physics engine for accurate movement, collision detection, and gravity simulation.
- **High-Performance NBT Parsing:** Fast, allocation-conscious Named Binary Tag (NBT) reading and writing.
- **Microsoft/Xbox Authentication:** Native support for modern Minecraft account authentication flows (`MinecraftProtoNet.Core.Auth`).
- **Extensible Command System:** A built-in registry for parsing and executing chat commands dynamically.

### 🗺️ Autonomous Pathfinding (`MinecraftProtoNet.Baritone`)
Inspired by the popular Baritone mod, this module provides server-independent, autonomous navigation and interaction:
- **A* Pathfinding:** Calculates optimal routes across complex terrain, considering block breaking, placing, and jumping costs.
- **Process Automation:** Built-in processes for common tasks including mining, farming, exploring, following entities, and navigating to specific coordinates.
- **Environment Interaction:** Safely interacts with the world (breaking blocks, placing blocks, opening chests).
- **Inventory & Look Behaviors:** Manages the bot's inventory automatically (e.g., swapping tools) and handles smooth, human-like camera movement.
- **World Caching:** Efficiently caches chunk data to allow pathfinding through previously seen, but currently unloaded, areas.

### 📈 Hypixel Bazaar Automation (`MinecraftProtoNet.Bazaar`)
A specialized module designed for interacting with the Hypixel Skyblock economy:
- **Algorithmic Trading:** Automates buy and sell orders based on market conditions and configured strategies.
- **High-Frequency API Integration:** Consumes the Hypixel Bazaar API to make real-time trading decisions.
- **Safety & Rate Limiting:** Built-in safeguards to prevent API bans and protect in-game currency.
- **Order Management:** Tracks active orders, fills, and historical performance.

### 🌐 Web Dashboard (`Bot.Webcore`)
A rich, real-time control panel built with Blazor Server:
- **Live Status Monitoring:** View the bot's health, coordinates, loaded chunks, and current active processes.
- **Interactive Inventory:** Real-time visual representation of the bot's inventory with full drag-and-drop support for moving items.
- **Chat Interface:** Read incoming server chat and send messages or commands directly from the browser.
- **Dynamic Configuration:** Adjust bot settings, trading strategies, and pathfinding parameters on the fly.

---

## 📁 Project Structure

| Project | Description |
|---------|-------------|
| `MinecraftProtoNet.Core` | The main engine: connection, physics, game state, chunk loading, and packets. |
| `MinecraftProtoNet.Core.Auth` | Handles Microsoft/Xbox OAuth login and session tokens. |
| `MinecraftProtoNet.Core.NBT` | High-performance NBT (Named Binary Tag) parsing. |
| `MinecraftProtoNet.Baritone` | Advanced A* pathfinding, goal setting, and environment processing. |
| `MinecraftProtoNet.Bazaar` | Automated trading logic and API integration for Hypixel Skyblock. |
| `Bot.Webcore` | The Blazor Server application serving as the UI dashboard. |
| `MinecraftProtoNet.Tests` | xUnit test suite ensuring protocol and behavioral accuracy. |
| `Tools/PacketIdSync` | Utility tool for syncing packet IDs across different Minecraft versions. |

## 🛠️ Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Running the Bot
1. **Clone the repository:**
   ```bash
   git clone https://github.com/your-username/MinecraftProtoNet.git
   cd MinecraftProtoNet
   ```
2. **Configure:** Edit `Bot.Webcore/appsettings.json` with your account details and target server IP.
3. **Run the Dashboard:**
   ```bash
   dotnet run --project Bot.Webcore
   ```
4. **Interact:** Open your browser and navigate to the web dashboard (e.g., `http://localhost:5000`) to control the bot.

## 🏗️ Architecture & Standards
This framework is built with a focus on performance and modern .NET idioms:
- **C# 14 Syntax:** Extensive use of file-scoped namespaces, primary constructors, and collection expressions.
- **Asynchronous Design:** Non-blocking async/await pipelines with strict `CancellationToken` propagation.
- **Dependency Injection:** Highly decoupled architecture using `Microsoft.Extensions.DependencyInjection`.
- **Performance:** Structs, `ref`, `in`, and `Span<T>` are utilized in hot paths (like NBT parsing and chunk reading) to minimize allocations.

## 🤝 Contributing
Contributions are welcome. Please ensure new code adheres to the existing C# 14 standards, utilizes primary constructors, and includes xUnit tests for new behaviors.

## 📄 License
[MIT License](LICENSE)
