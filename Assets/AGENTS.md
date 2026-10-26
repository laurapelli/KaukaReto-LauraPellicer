# Development conventions

## Architecture

* Prefer simple, maintainable solutions with clear responsibilities.
* Keep gameplay logic modular and separate it from presentation and input handling.
* Keep enemy decision-making separate from player control and shared combat behavior.
* Reuse common behavior instead of duplicating logic across actors.
* Use clear names that describe the purpose of classes, fields, and scene objects.
* When requirements leave implementation details open, prefer deterministic analytical approaches over costly simulations. For enemy ballistic aiming, an analytical trajectory solution is preferred when it simplifies targeting and makes results reproducible.
* Expose relevant tuning values through serialized configuration rather than scattering constants through the code.
* Control enemy difficulty primarily through shooting accuracy and configurable aiming error, keeping those parameters separate from presentation.
* Keep AI turn ownership active through one post-resolution ballistic correction when the previous projectile outcome can materially improve the current firing solution. The correction should reuse the existing movement and aiming context rather than restarting the complete turn sequence.
* Retain successful enemy ballistic solutions and reuse them for sufficiently similar engagement distances when doing so improves consistency between turns.
* Keep actor state transitions explicit and avoid coupling turn progression directly to animation timing or presentation events.

## Movement and navigation

* Keep tank movement deterministic and independent from engine-specific baked navigation data.
* Build movement paths from explicit walkable-space sampling and physics obstacle queries so navigation behavior can be reproduced directly from scene geometry.
* Keep navigation logic independent from visual presentation and tank animation.
* Validate candidate destinations against movement range, traversable space, and nearby obstacles before committing to movement.
* Keep path planning and path execution separate so either can be adjusted without changing the other.
* Avoid embedding scene-specific coordinates or paths directly in gameplay code.

## Unity practices

* Use serialized references for known scene and prefab dependencies where practical.
* Cache frequently used components and avoid unnecessary object searches in Update.
* Avoid unnecessary allocations in per-frame code and reuse temporary collections where appropriate.
* Use Unity physics for collision detection and keep physics updates consistent with the chosen Rigidbody configuration.
* Preserve projectile impact velocity through collision resolution and use it as part of hit effectiveness evaluation so combat responses remain consistent with the physical outcome of the shot.
* Apply layers consistently and make filtering explicit for physics queries.
* Keep coordinate spaces explicit when working with transforms and directions.
* Use dedicated transforms for rotation pivots, spawn positions, attachment points, and other spatial references instead of relying on mesh origins where practical.
* Keep initialization and cleanup balanced, including event subscriptions.
* Keep editor-only code separate from runtime code.

## AI behavior

* Keep enemy decision-making deterministic where possible so behavior can be reproduced and tuned.
* Separate target selection, movement decisions, aiming decisions, and firing execution into clearly defined responsibilities.
* Prefer configurable behavioral parameters over hard-coded difficulty branches.
* Allow aiming decisions to make use of information obtained from previous projectile outcomes when refining subsequent firing solutions.
* Avoid perfect information where it is unnecessary for gameplay behavior, but preserve enough previous-shot context to improve consistency.
* Keep AI-specific behavior isolated from shared tank mechanics so changes to enemy decision-making do not affect player controls.

## Assets and workflow

* Preserve the supplied art, materials, audio, and visual style.
* Modify supplied assets only when necessary for the requested behavior.
* Preserve asset references and their associated meta files when moving or renaming assets.
* Prefer focused prefab changes with clear transform hierarchies.
* Keep changes scoped to the task and avoid unrelated dependency or project-setting updates.
* Avoid replacing supplied assets when the required behavior can be implemented through composition or configuration.
* Check the resulting behavior in Play Mode, including different positions, movement destinations, firing powers, and aiming angles.
* Test interactions near obstacles and scene boundaries rather than validating only ideal movement and firing conditions.
