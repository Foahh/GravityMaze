#include "lib/MPU9250.h"

MPU9250 mpu;
MPU9250Setting imu_setting;  // Global settings for reconnection
bool mpu_connected = false;
uint32_t last_reconnect_attempt = 0;
const uint32_t RECONNECT_INTERVAL = 2000;  // Try reconnecting every 2 seconds
uint32_t consecutive_update_failures = 0;
const uint32_t DISCONNECT_THRESHOLD = 50;  // Consider disconnected after 50 failed updates

void setup() {
  Serial.begin(115200);
  Wire.begin();
  delay(2000);

  // Optimized settings for hand-held cube tracking
  imu_setting.accel_fs_sel = ACCEL_FS_SEL::A4G;           // ±4g - good balance for hand movements
  imu_setting.gyro_fs_sel = GYRO_FS_SEL::G500DPS;         // ±500 deg/s - captures fast rotations with good resolution
  imu_setting.mag_output_bits = MAG_OUTPUT_BITS::M16BITS; // 16-bit magnetometer for drift correction
  imu_setting.fifo_sample_rate = FIFO_SAMPLE_RATE::SMPL_200HZ; // 200Hz - sufficient for hand tracking
  imu_setting.gyro_fchoice = 0x03;
  imu_setting.gyro_dlpf_cfg = GYRO_DLPF_CFG::DLPF_41HZ;   // 41Hz LPF - smooths hand tremor
  imu_setting.accel_fchoice = 0x01;
  imu_setting.accel_dlpf_cfg = ACCEL_DLPF_CFG::DLPF_45HZ; // 45Hz LPF - reduces jitter

  // Initial connection attempt
  Serial.println("Attempting initial MPU connection...");
  mpu_connected = connectMPU();
  
  if (mpu_connected) {
    Serial.println("MPU connected successfully!");
  } else {
    Serial.println("Initial MPU connection failed. Will retry...");
  }
}

bool connectMPU() {
  if (mpu.setup(0x68, imu_setting)) {
    consecutive_update_failures = 0;
    return true;
  }
  return false;
}

void reconnectMPU() {
  uint32_t current_ms = millis();
  
  // Don't attempt reconnection too frequently
  if (current_ms - last_reconnect_attempt < RECONNECT_INTERVAL) {
    return;
  }
  
  last_reconnect_attempt = current_ms;
  Serial.println("Attempting to reconnect MPU...");
  
  // Reinitialize I2C bus
  Wire.end();
  delay(100);
  Wire.begin();
  delay(100);
  
  // Try to reconnect
  if (connectMPU()) {
    Serial.println("MPU reconnected successfully!");
    mpu_connected = true;
  } else {
    Serial.println("Reconnection failed. Will retry...");
  }
}

void loop() {
  // If not connected, try to reconnect
  if (!mpu_connected) {
    reconnectMPU();
    return;
  }
  
  // Try to update sensor data
  if (mpu.update()) {
    consecutive_update_failures = 0;  // Reset failure counter on success
    
    static uint32_t prev_ms = 0;
    uint32_t current_ms = millis();
    if (current_ms - prev_ms >= 10) {
      get_quaternion();
      prev_ms = current_ms;
    }
  } else {
    // Update failed - increment failure counter
    consecutive_update_failures++;
    
    // If we've had too many consecutive failures, consider the device disconnected
    if (consecutive_update_failures >= DISCONNECT_THRESHOLD) {
      Serial.println("MPU appears to be disconnected!");
      mpu_connected = false;
      consecutive_update_failures = 0;
    }
  }
}


inline void get_quaternion() {
  // Use quaternions for unlimited rotation (no angle wrapping)
  float qw = mpu.getQuaternionW();
  float qx = mpu.getQuaternionX();
  float qy = mpu.getQuaternionY();
  float qz = mpu.getQuaternionZ();

  Serial.print("QW:");
  Serial.print(qw, 6);
  Serial.print(",QX:");
  Serial.print(qx, 6);
  Serial.print(",QY:");
  Serial.print(qy, 6);
  Serial.print(",QZ:");
  Serial.print(qz, 6);
  Serial.println();
}