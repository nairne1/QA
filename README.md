# AI QA Testing Project



### Project Overview

This project explores the use of Artificial Intelligence as a Quality Assurance (QA) tool within a 2D platformer.

A reinforcement learning agent (Unity ML-Agent) is trained to explore a level and identify seeded bugs.

This project compares AI-based testing with human testing to evaluate effectiveness.



### How to Run the Project

1. Download the project from GitHub (code->Download Zip).
2. Unzip the folder.
3. Download Unity Hub and Unity version 6000.0.60f1.
4. Click Add->Add Project from Disk and select the extracted project folder.
5. Open the project, this may take some time the first time you do it.
6. Once opened, open the MainMenu scene (Assets->Scenes->MainMenu) and press play.

The warnings in the console are just adding components to objects that require them, they can be ignored.



### Agent Models Breakdown

Trained AI: fully trained model demonstrating learned exploration and bug detection behaviour.

Training in Progress: shows intermediate behaviour during training, highlighting learning progression.

Untrained AI: demonstrates initial random behaviour before learning occurs.



### AI Training Breakdown

The agent was trained externally using Unity ML-Agents with Python (Anaconda environment).

Training was run using:



mlagents-learn Behaviour/PlayerAgent1.yaml --run-id=trained



The Unity scene must be running while executing this command.

Note: Recreating the training environment is not required to run this project, as trained models are included.



### What to Observe

* How the trained AI explores the level
* How it detects and triggers bugs
* Differences between trained and untrained behaviour
* Comparison with human testing performance



### Scene Breakdown

#### Main Menu:

* Navigation hub for seeing the different trained AI models.



#### Untrained AI Scene:

* Shows the agent before training.
* Behaviour appears random and inefficient.



#### Trained AI Scene:

* Shows fully trained agent.
* Agent explores the level and attempts to discover bugs.



#### Training Progress Scene:

* Shows training development.



#### Human Testing Scene:

* Shows what human testers have access to.
* They had the same controls as the AI agent, and they could manually report the bugs they encountered.
* Controls:

  * 'A' and 'D', or left and right arrow keys to move left and right.
  * Space to jump.
  * Esc or 'P' to pause the game.
  * 'J' to show object hitboxes, and 'H' to show player heatmap.



### Project Aim

The aim of this project is to investigate whether reinforcement learning can be used as an effective QA tool for detecting bugs in game development.



### AI System Overview

The AI agent is trained using reinforcement learning. It learns by interacting with the environment and receiving rewards for:

* Exploring new areas
* Triggering bugs

The agent is then penalised for:

* Dying
* Moving away from the nearest walkable area
* Being 'stuck' (staying in the same general area for too long)



### Training Method

Training was performed externally using Unity ML-Agents and Python (via Anaconda PowerShell). The Unity project acted as the simulation environment, while training was controlled through python.



### Key Systems and Scripts

#### SimplifiedCoverage.cs

* Core reinforcement learning agent.
* Handles:

  * movement
  * jumping
  * decision making
  * reward assignment



#### GridCoverageTracker2D

* Tracks which areas of the level have been explored.
* Used to reward/penalise exploration behaviour.
* When used in the scene, it has visualisation to help see where the agent has been - yellow if the cell has not been visited yet, and green if it has.



### HeatmapTracker.cs

* Generates visual representation of agent/human movement.
* Used for analysis of exploration patterns.
* The heatmap is saved in the Heatmaps folder.



#### Bug/Trigger Scripts

* Control the behaviour of the seeded bugs.
* Examples:

  * Failed goal trigger (random failure chance)
  * Missing collider (player falls through)
  * Soft lock (player becomes stuck)
  * Faulty collectible (does not register correctly)



#### RunLogger/Data Logging

* For AI, it records:

  * time it took to find bug
  * position of agent
  * bug id
* For human testing, they can manually record:

  * bug title
  * expected result
  * actual result
  * steps to reproduce bug
  * severity
  * it automatically logs the session time, position of player, death count, and score.

