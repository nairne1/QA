# AI QA Testing Project



### Project Overview

This project explores the use of Artificial Intelligence as a Quality Assurance (QA) tool within a 2D platformer.

A reinforcement learning agent (Unity ML-Agent) is trained to explore a level and identify seeded bugs.

This project compares AI-based testing with human testing to evaluate effectiveness.



### How to Run the Project

Launch the exe

The main menu should appear, select the mode you wish to see: trained, untrained, in progress.



### How I Trained the Agent

I used Anaconda Powershell to train my agent.

Here's what I typed into the prompt:

1\. conda activate mlagents

2\. make sure directory is correct, e.g. cd "C:\\Users\\nairn\\Documents\\Turtle Agent"

3\. mlagents-learn --run-id=agent1

4\. if using yaml file: mlagents-learn Behaviour\\PlayerAgent1.yaml --run-id=agent1





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



### Project Aim

The aim of this project is to investigate whether reinforcement learning can be used as an effective QA tool for detecting bugs in game development.



### AI System Overview

The AI agent is trained using reinforcement learning. It learns by interacting with the environment and receiving rewards for:

* Exploring new areas
* Triggering bugs

The agent is then penalised for:

* Dying
* Moving away form the nearest walkable area
* Being 'stuck' (staying in the same general area for too long)



### Training Method

Training was performed externally using Unity ML-Agents and Python (via Anaconda Powershell). The Unity project acted as the simulation environment, while training was controlled through python.



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



### HeatmapTracker.cs

* Generates visual representation of agent/human movement.
* Used for analysis of exploration patterns.



#### Bug/Trigger Scripts

* Control the behaviour of the seeded bugs.
* Examples:

  * broken triggers
  * faulty collectibles
  * detection issues



#### RunLogger/Data Logging

* For AI, it records:

  * time it took to find bug
  * position of agent
  * bug id
* For human they can manually record:

  * bug title
  * expected result
  * actual result
  * steps to reproduce bug
  * severity
  * it automatically logs the session time, position of player, death count, and score.

