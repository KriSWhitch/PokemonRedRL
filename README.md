# Pokémon Red Reinforcement Learning Project

![Pokémon Red](https://img.icons8.com/color/96/000000/pokeball--v1.png)

## Project Overview

This project implements a Reinforcement Learning (RL) agent that learns to play Pokémon Red (Game Boy) using the mGBA emulator. The system consists of three main components:

1. **Lua Script** - Acts as a bridge between the emulator and .NET application
2. **.NET Core 8.0 Application** - Main AI logic and model training
3. **TorchSharp** - Deep learning framework for the RL model

## Key Features

- 🕹️ Real-time interaction with Pokémon Red game in the mGBA emulator via Lua scripts
- 🧠 Custom DQN (Deep Q-Network) implementation using TorchSharp  
- 🔄 Experience replay buffer for stable training  
- 📊 State tracking including player position, HP, and map location  
- 🎮 Support for all 8 basic game actions (Up, Down, Left, Right, A, B, Start, Select)  

## Project Structure

    PokemonRedRL
    ├── PokemonRedRL.Agent      # Main application and RL agent
    ├── PokemonRedRL.Core       # Core emulator interaction logic
    │   ├── Emulator               
    │   └── Enums  
    ├── PokemonRedRL.Models     # Neural network models and training
    └── scripts
        └── mgba_socket.lua     # Lua bridge script


## Technical Stack

| Component          | Technology             |
|--------------------|------------------------|
| Emulator           | mGBA 0.10.5            |
| Bridge             | Lua 5.4.7 + luasocket  |
| Core Application   | .NET 8.0               |
| Machine Learning   | TorchSharp             |
| Protocol           | Custom TCP             |
