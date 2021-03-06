﻿using UnityEngine;
using UnityEngine.UI;

namespace LD41
{
	[RequireComponent(typeof(Rigidbody2D))]
	[RequireComponent(typeof(Collider2D))]
	[RequireComponent(typeof(Animator))]
	public class PlayerCharacter : MonoBehaviour, IPoolableObject
	{
		[SerializeField]
		private SpriteRenderer GunArmSprite;
		[SerializeField]
		private SpriteRenderer BodySprite;

		[SerializeField]
		private Image LaserSight;

		public Transform ShootiePoint;

		public float RotationPerSecond = 2;
		public float AccelerationPerSecond = 0.5f;
		public float MaxSpeed = 2;

		public int PlayerID = 0;
		public int TeamID = 0;
		private PlayerInput playerInput;

		private Rigidbody2D _rigidbody;
		[SerializeField]
		private ParticleSystem ParticleSystem;
		private Animator _animator;

		[SerializeField]
		private Image HPBar;

		[SerializeField]
		private Image DashMeter;
		[SerializeField]
		private Image DashBar;

		[SerializeField]
		private Image AimBar;

		public float ShotCoolDown = 0.5f;
		private float _lastShot = 0;

		public float MaxHealth = 10;
		public float CurrentHealth = 10;

		public void SetInfo(int playerID, int teamID, Color teamColor)
		{
			PlayerID = playerID;
			TeamID = teamID;

			var customColor = ((playerID / 4f) * 0.5f + 0.5f) * teamColor;

			BodySprite.color = customColor;
			LaserSight.color = customColor;
			gameObject.layer = LayerMask.NameToLayer($"team{TeamID}");
		}

		private void OnEnable()
		{
			_rigidbody = GetComponent<Rigidbody2D>();
			_animator = GetComponent<Animator>();
			UpdateHealthBar();
		}

		public void TakeDamageFrom(PlayerCharacter damageCauser, float amount = 1)
		{
			CurrentHealth -= amount;
			UpdateHealthBar();
			if (CurrentHealth <= 0)
			{
				Die(damageCauser);
			}
			else
			{
				GameManager.Instance.PlaySound("hurt");
			}
		}

		public void Die(PlayerCharacter murderer)
		{
			GameManager.Instance.PlaySound("death");
			GameManager.Instance.AddScoreFor(murderer);
			GameManager.Instance.RespawnPlayer(this);
			gameObject.SetActive(false);
		}

		public void UpdateHealthBar()
		{
			HPBar.rectTransform.localScale = new Vector3(CurrentHealth / (float)MaxHealth, 1, 1);
		}

		public float lastDash;
		public float DashDuration = 0.5f;
		public float DashCooldown = 2f;
		public bool CanDash => (Time.time - lastDash) > DashCooldown;
		public float DashSpeed = 10f;
		public bool IsDashing;
		private void Update()
		{
			if (GameManager.IsGamePaused) return;

			// Get input associated with the player's controller
			playerInput = InputHelper.GetPlayerInput(PlayerID);

			if (IsDashing)
			{
				_rigidbody.velocity = transform.up * DashSpeed * CONST.PIXELS_PER_UNIT;
				_animator.SetBool("moving", true);
			}
			else
			{
				if (!CanDash)
				{
					DashBar.rectTransform.localScale = new Vector3((Time.time - lastDash) / DashCooldown, 1, 1);
				}

				if (playerInput.HorizontalMovement != 0)
				{
					_rigidbody.angularVelocity = 0;
					var amountToRotate = -playerInput.HorizontalMovement * RotationPerSecond * Time.deltaTime;
					var newAngle = transform.localRotation.eulerAngles.z + amountToRotate;
					transform.localRotation = Quaternion.Euler(new Vector3(0, 0, newAngle));
				}

				if (playerInput.Dashing && CanDash)
				{
					lastDash = Time.time + DashDuration;
					IsDashing = true;
					GameManager.Instance.PlaySound("superthrusty");
				}

				var aimVec = playerInput.GetNormalizedAim(transform.position, Camera.main);
				if (aimVec.x != 0 || aimVec.y != 0)
				{
					GunArmSprite.transform.eulerAngles = new Vector3(0, 0, Mathf.Atan2(aimVec.y, aimVec.x) * Mathf.Rad2Deg);
					var gangle = GunArmSprite.transform.localEulerAngles.z;
					if (gangle >= 90 && gangle <= 270)
					{
						BodySprite.transform.localScale = new Vector3(-1, 1, 1);
						GunArmSprite.transform.localScale = new Vector3(1, -1, 1);
					}
					else
					{
						BodySprite.transform.localScale = new Vector3(1, 1, 1);
						GunArmSprite.transform.localScale = new Vector3(1, 1, 1);
					}
				}

				if (playerInput.Shooting)
				{
					if (Time.time - _lastShot > ShotCoolDown)
					{
						_lastShot = Time.time;
						var bullet = (Projectile)GameManager.Instance.GetBullet();
						bullet.transform.position = ShootiePoint.position;
						bullet.SetDir(GunArmSprite.transform.right);
						bullet.Player = this;
						_rigidbody.velocity += (Vector2)GunArmSprite.transform.right * -CONST.PIXELS_PER_UNIT;
						bullet.gameObject.layer = gameObject.layer;
						GameManager.Instance.PlaySound("shootie");
					}
				}

				if (playerInput.Jump)
				{
					var velocity = _rigidbody.velocity;
					velocity += (Vector2)transform.up * AccelerationPerSecond * CONST.PIXELS_PER_UNIT;

					var maxSpeed = MaxSpeed * CONST.PIXELS_PER_UNIT;
					if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
					{
						velocity = velocity.normalized * maxSpeed;
					}

					_rigidbody.velocity = velocity;

					ParticleSystem.Emit(1);
					_animator.SetBool("moving", true);
				}
				else
				{
					_animator.SetBool("moving", false);
				}
			}

			// Aim Bar
			var hits = Physics2D.RaycastAll(ShootiePoint.position, GunArmSprite.transform.right, 1000);
			if (hits.Length > 0)
				foreach (var hit in hits)
				{
					if (hit.collider.gameObject == this.gameObject) continue;
					if (hit.collider.gameObject.tag == "shooties") continue;

					var hitDist = hit.distance;
					AimBar.rectTransform.localScale = new Vector3(hitDist / 1000f, 1, 1);
					break;
				}
			else
			{
				AimBar.rectTransform.localScale = new Vector3(1, 1, 1);
			}
		}

		public void ReturnToPool()
		{
			_rigidbody.velocity = Vector2.zero;
			gameObject.SetActive(false);
		}

		public void ActivateFromPool()
		{
			CurrentHealth = MaxHealth;
			gameObject.SetActive(true);
		}

		private void OnCollisionEnter2D(Collision2D collision)
		{
			if (IsDashing)
			{
				if (collision.gameObject.tag == "PlayerBoy"
					&& collision.gameObject.layer != gameObject.layer)
				{
					collision.gameObject.GetComponent<PlayerCharacter>().TakeDamageFrom(this, 2);
				}

				IsDashing = false;

				GameManager.Instance.PlaySound("collide");
			}
		}
	}
}
