# syntax=docker/dockerfile:1

# ── STAGE 1: builder ──────────────────────────────────────────────────────────
FROM ubuntu:24.04 AS builder

ENV DEBIAN_FRONTEND=noninteractive
ENV LANG=C.UTF-8

# OpenMVG dependencies
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
    && apt-get update && apt-get install -y build-essential cmake git wget curl pkg-config \
    libeigen3-dev libopencv-dev libtbb-dev \
    libceres-dev libflann-dev libcgal-dev \
    libgmp-dev libmpfr-dev libboost-all-dev \
    libpng-dev libjpeg-dev libwebp-dev libtiff-dev \
    libgl1-mesa-dev libglu1-mesa-dev libglib2.0-0 \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# 2. Build OpenMVG
WORKDIR /opt
RUN git clone --recursive -b master https://github.com/openMVG/openMVG.git && \
    cd openMVG && \
    mkdir build && cd build && \
    cmake ../src \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX=/usr/local \
        -DOPENMVG_BUILD_TESTS=OFF \
        -DOPENMVG_BUILD_EXAMPLES=OFF \
    && make -j$(nproc) && \
    make install

# 3. Build OpenMVS
RUN apt-get update && \
    apt-get install -y --no-install-recommends ca-certificates \
        curl zip unzip tar \
        git python3 python3-venv python3-jinja2 cmake build-essential pkg-config libtool bison autoconf autoconf-archive automake \
        libavcodec-dev libavformat-dev libavutil-dev libswscale-dev libswresample-dev \
        libxi-dev libx11-dev libxft-dev libxtst-dev libxext-dev libxrandr-dev \
        libxinerama-dev libxcursor-dev xorg-dev libgl-dev libglu1-mesa-dev \
        nasm gfortran libltdl-dev && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /opt
RUN git clone https://github.com/microsoft/vcpkg.git && \
    /opt/vcpkg/bootstrap-vcpkg.sh -disableMetrics

ENV VCPKG_ROOT=/opt/vcpkg
ENV PATH="${VCPKG_ROOT}:${PATH}"

WORKDIR /app
RUN git clone --recurse-submodules -b master https://github.com/svilenvarbanov2019/openMVS.git

WORKDIR /app/openMVS/make

# Build OpenMVS
RUN cmake .. \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX=/usr/local \
    -DCMAKE_TOOLCHAIN_FILE=/opt/vcpkg/scripts/buildsystems/vcpkg.cmake \
    -DOpenMVS_BUILD_VIEWER=OFF \
    -DOpenMVS_USE_PYTHON=OFF \
    -DOpenMVS_USE_CUDA=OFF \
    -DOpenMVS_USE_OPENGL=OFF \
    -DOpenMVS_ENABLE_TESTS=OFF

RUN cmake --build . -j4
RUN cmake --install .

# ── STAGE 2: runtime ──────────────────────────────────────────────────────────
FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive
ENV LANG=C.UTF-8

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        # Boost (OpenMVS runtime)
        libboost-iostreams1.83.0 \
        libboost-program-options1.83.0 \
        libboost-serialization1.83.0 \
        # OpenCV (OpenMVG + OpenMVS runtime) — pulls libjpeg/libtiff/libpng transitively
        libopencv-core406t64 \
        libopencv-imgproc406t64 \
        libopencv-imgcodecs406t64 \
        libwebp7 \
        # Other OpenMVG runtime deps
        libtbb12 \
        libflann1.9 \
        libceres4 \
        libgfortran5 \
        # Media / display
        ffmpeg \
        libx11-6 libgl1 libglu1-mesa && \
    rm -rf /var/lib/apt/lists/*

# Copy installed OpenMVG & OpenMVS binaries and libraries from the builder
COPY --from=builder /usr/local/bin/ /usr/local/bin/
COPY --from=builder /usr/local/lib/ /usr/local/lib/
COPY --from=builder /usr/local/include/ /usr/local/include/

RUN echo "/usr/local/lib/OpenMVS" > /etc/ld.so.conf.d/openmvs.conf && ldconfig
ENV PATH=/usr/local/bin/OpenMVS:$PATH

RUN ldconfig