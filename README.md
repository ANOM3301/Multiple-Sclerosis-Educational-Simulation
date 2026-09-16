# Multiple Sclerosis Educational Simulation

An interactive Unity-based simulation designed to demonstrate how different signs and symptoms of Multiple Sclerosis (MS) can affect everyday activities.

## About the Project

This project was created as an educational tool for a health science presentation. The simulation places the user in the perspective of a student completing a written test while experiencing several symptoms associated with MS.

Rather than simply explaining symptoms, the simulation allows users to experience visual, motor, sensory, and cognitive disruptions through interactive gameplay.

## Simulated Symptoms

The simulation currently demonstrates six MS-related signs and symptoms:

- **Optic Neuropathy / Optic Neuritis**
  - Simulated through visual disturbances while looking around.

- **Nystagmus**
  - Simulated through involuntary camera movement.

- **Fine Motor Tremor**
  - Simulated through movement of the pencil and writing.

- **Cognitive Fatigue**
  - Temporarily interrupts the user's ability to continue writing.

- **Sensory Paresthesia**
  - Temporarily prevents the user from writing to simulate numbness or tingling.

- **Lhermitte's Sign**
  - Triggered by looking upward and represented through a visual effect.

## Features

- Interactive first-person environment
- Virtual mouse cursor for writing
- Six-question written test
- Real-time symptom activation
- Pencil movement and writing simulation
- Randomized sensory and cognitive interruptions
- Camera-based nystagmus effect
- Visual symptom effects
- MS symptoms can be activated or deactivated
- Designed for classroom demonstration

## How It Works

The simulation uses Unity scripts to control the environment, camera, pencil, and symptom effects.

### Writing System

The user controls a virtual cursor while the physical mouse cursor remains locked. A raycast from the test camera detects the paper and allows the user to draw using a `LineRenderer`.

### Fine Motor Tremor

Perlin noise is used to create subtle irregular movement in the pencil and writing path. This creates a more natural tremor effect rather than simple repetitive shaking.

### Cognitive Fatigue

While MS symptoms are active, cognitive fatigue can randomly occur. During an episode, writing is temporarily interrupted before normal interaction resumes.

### Sensory Paresthesia

Random sensory interruptions temporarily prevent the user from drawing, representing how numbness or tingling may interfere with fine motor activities.

### Nystagmus

The camera receives a rapid oscillating rotation to simulate involuntary eye movement and the resulting visual instability.

### Lhermitte's Sign

Looking upward triggers a brief visual effect representing the sudden sensory disturbance associated with Lhermitte's sign.

## Project Structure

The main scripts include:

- `MSController.cs`
  - Controls whether MS symptoms are active.

- `MoveWithMouse.cs`
  - Controls camera movement, nystagmus, and Lhermitte's sign.

- `testScript.cs`
  - Controls the written test, virtual cursor, pencil, drawing system, tremor, cognitive fatigue, and sensory interruptions.

## Technologies

- **Unity**
- **C#**
- **Unity Input System**
- **Line Renderer**
- **Physics Raycasting**
- **Unity UI**

## Educational Purpose

This project is intended to help students better understand how MS symptoms can affect everyday tasks.

The simulation is **not a medical diagnostic tool** and does not attempt to reproduce the experience of MS exactly. Symptoms vary significantly between individuals, and the simulation represents selected symptoms for educational purposes.

## Presentation

The simulation is accompanied by a presentation covering:

- What Multiple Sclerosis is
- How MS affects the nervous system
- Signs and symptoms of MS
- Demonstrations of the simulated symptoms
- A pre-presentation knowledge survey
- A post-presentation knowledge survey

The surveys are used to compare participants' understanding of MS before and after the presentation.

## Future Improvements

Potential future additions include:

- Additional MS symptoms
- More interactive activities
- Improved visual effects
- More detailed symptom customization
- Additional accessibility options
- Expanded educational scenarios
- Data collection from classroom demonstrations

## Disclaimer

This project is an educational simulation created to demonstrate selected MS symptoms. It should not be used to diagnose, evaluate, or represent the experience of any individual with Multiple Sclerosis.
