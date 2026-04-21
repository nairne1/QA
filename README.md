# AI QA Testing Project



### Project Overview

This project explores the use of Artificial Intelligence as a Quality Assurance (QA) tool within a 2D platformer.

A reinforcement learning agent (Unity ML-Agent) is trained to explore a level and identify seeded bugs.

This project compares AI-based testing with human testing to evaluate effectiveness.



### How to Run the Project

Launch the exe.

The main menu should appear, select the mode you wish to see: trained, untrained, in progress.



### How to Train the Agent

I used Anaconda Powershell to train my agent. I used Unity version 6000.0.60f1, Python version 3.14.0, numpy 1.23.5 (used to organise and manipulate all the numerical data the agent learns from), and torch 2.2.1 (learns the behaviour of the agent)

Here's what I typed into the prompt:

1\. Create a new Conda environment with Python 3.14.0

conda create -n mlagents python=3.14.0



2\. Activate the environment

conda activate mlagents



3\. Install necessary packages

conda install numpy=1.23.5

pip3 install torch\~=2.2.1 --index-url https://download.pytorch.org/whl/cu121



4\. Start Python to verify installation

python

import torch

import numpy



You can recheck the version is correct with print(torch.\_\_version) and print(numpy.\_\_version)



exit()



4.5. Clear the terminal (optional)

clear





5\. Change directory to ML-Agents folder

example: cd D:\\Unity\\ml-agents



6\. Install ML-Agents from the local source files

python -m pip install ./ml-agents-envs

python -m pip install ./ml-agents



7\. Check ML-Agents installation

mlagents-learn --help



8\. Start a training session for the Basic environment

mlagents-learn config/ppo/Basic.yaml --run-id=run1



8.5. Force overwrite or resume previous run

mlagents-learn config/ppo/Basic.yaml --run-id=run1 --force

mlagents-learn config/ppo/Basic.yaml --run-id=run1 --resume



In-order to see the Tensorboard graphs, write this in another Powershell Prompt:



1. conda activate mlagents

2\. cd C:\\Users\\nairn\\Documents\\QA

3\. tensorboard --logdir=results





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

